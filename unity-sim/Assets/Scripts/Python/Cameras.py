import platform
import time
import threading
import os
import cv2
import numpy as np
import yaml
from typing import List, Tuple, Optional, Dict, Any
from dataclasses import dataclass

@dataclass
class cam_config:
    name: str
    device: str
    out_dir: str

class camera_worker:
    def __init__(self, cfg: cam_config, size: Tuple[int, int], fps: int, mjpg: bool, jpeg_quality: int):
        self.cfg = cfg  
        self.size = size
        self.fps = int(fps)
        self.mjpg = mjpg
        self.jpeg_quality = int(jpeg_quality)

        self.cap: Optional[cv2.VideoCapture] = None
        self.lock = threading.Lock()

        self.latest: Optional[np.ndarray] = None
        self.latest_jpeg: Optional[bytes] = None
        self.actual_width = 0
        self.actual_height = 0
        self.actual_fps = 0.0
        self.frame_count = 0
        self.fps_start_time = 0.0

        self.last_ok_ts = 0.0
        self.running = False
        self.thread: Optional[threading.Thread] = None

        os.makedirs(self.cfg.out_dir, exist_ok=True)

    def start(self) -> None:
        # Select appropriate backend based on OS
        if platform.system() == "Windows":
            # Windows: use DirectShow, device should be an integer index
            try:
                device_idx = int(self.cfg.device)
            except ValueError:
                raise RuntimeError(f"{self.cfg.name}: on Windows, device must be an integer (0, 1, 2, ...), got {self.cfg.device}")
            self.cap = cv2.VideoCapture(device_idx, cv2.CAP_DSHOW)
            # self.cap = cv2.rotate(self.cap, cv2.ROTATE_180)

        else:
            # Linux: use V4L2
            device_idx = int(self.cfg.device) if self.cfg.device.isdigit() else self.cfg.device
            self.cap = cv2.VideoCapture(device_idx, cv2.CAP_V4L2)
        
        if not self.cap.isOpened():
            raise RuntimeError(f"{self.cfg.name}: failed to open {self.cfg.device}")

        w, h = self.size
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, float(w))
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, float(h))
        self.cap.set(cv2.CAP_PROP_FPS, float(self.fps))
        if self.mjpg:
            self.cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))

        try:
            self.cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        except Exception:
            pass

        self.running = True
        self.thread = threading.Thread(target=self._loop, daemon=True)
        self.thread.start()

    def stop(self) -> None:
        self.running = False
        if self.thread:
            self.thread.join(timeout=2.0)
        if self.cap:
            self.cap.release()

    def _loop(self) -> None:
        time.sleep(0.2)

        period = 1.0 / max(1, self.fps)
        next_t = time.time()
        encode_params = [int(cv2.IMWRITE_JPEG_QUALITY), self.jpeg_quality]
        
        # Initialize FPS tracking
        self.fps_start_time = time.time()
        self.frame_count = 0

        while self.running:
            ok, frame = self.cap.read() if self.cap else (False, None)
            frame = cv2.rotate(frame, cv2.ROTATE_180)

            """
            ----------------------------------------------------------------
            Frame HERE!
            ----------------------------------------------------------------
            """
            if ok and frame is not None:
                # frame = cv2.rotate(frame, cv2.ROTATE_180)
                h, w = frame.shape[:2]
                ok2, buf = cv2.imencode(".jpg", frame, encode_params)
                jpeg = buf.tobytes() if ok2 else None

                with self.lock:
                    self.latest = frame
                    self.latest_jpeg = jpeg
                    self.actual_width = w
                    self.actual_height = h
                    self.frame_count += 1
                    
                    # Update FPS calculation every 30 frames
                    if self.frame_count % 30 == 0:
                        elapsed = time.time() - self.fps_start_time
                        if elapsed > 0:
                            self.actual_fps = 30.0 / elapsed
                        self.fps_start_time = time.time()
                    
                    self.last_ok_ts = time.time()
            else:
                time.sleep(0.005)

            next_t += period
            sleep = next_t - time.time()
            if sleep > 0:
                time.sleep(sleep)
            else:
                next_t = time.time()

    def get_jpeg(self) -> Optional[bytes]:
        with self.lock:
            return self.latest_jpeg

    def get_frame(self) -> Optional[np.ndarray]:
        with self.lock:
            return self.latest.copy() if self.latest is not None else None

    def save_burst(self, count: int = 10, spacing_ms: int = 0, out_dir: Optional[str] = None) -> Dict[str, object]:
        if out_dir is None:
            out_dir = self.cfg.out_dir
        os.makedirs(out_dir, exist_ok=True)

        saved = []
        errors = []

        for i in range(int(count)):
            with self.lock:
                frame = None if self.latest is None else self.latest.copy()

            if frame is None:
                errors.append({"i": i, "error": "no frame yet"})
            else:
                fname = f"{self.cfg.name}_{now_stamp()}_{int(time.time()*1000)%1000:03d}_b{i:02d}.png"
                path = os.path.join(out_dir, fname)
                ok = cv2.imwrite(path, frame)
                if ok:
                    saved.append(path)
                else:
                    errors.append({"i": i, "error": "cv2.imwrite failed"})

            if spacing_ms > 0:
                time.sleep(spacing_ms / 1000.0)

        return {"ok": (len(saved) > 0 and len(errors) == 0), "saved": saved, "errors": errors}

class undistort_cam:
    def __init__(
        self,
        name: str,
        device: str,
        intrinsics_path: str,
        model: str,
        out_size: Optional[Tuple[int, int]] = None,
        balance: float = 0.0,
        rotate_code: Optional[int] = None,
        mjpg: bool = True,
        capture_w: Optional[int] = None,
        capture_h: Optional[int] = None,
        capture_fps: Optional[int] = None,
    ):
        self.name = name
        self.device = device
        self.intrinsics_path = intrinsics_path
        self.out_size = out_size
        self.balance = float(balance)
        self.rotate_code = rotate_code
        self.mjpg = mjpg

        self.capture_w = capture_w
        self.capture_h = capture_h
        self.capture_fps = capture_fps

        self.model = model
        self.K, self.D, self.yaml_size = load_intrinsics(intrinsics_path)

        self.lock = threading.Lock()
        self.last_frame_bgr: Optional[np.ndarray] = None
        self.last_ok = False
        self.last_err = ""
        self.last_ts = 0.0

        # Timing measurements
        self.capture_time = 0.0
        self.undistort_time = 0.0
        self.encode_time = 0.0
        self.total_time = 0.0

        # FPS tracking (similar to capture.py)
        self.actual_fps = 0.0
        self.fps_start_time = 0.0
        self.frame_count = 0

        # Undistort maps will be initialized after we know input frame size
        self.map1 = None
        self.map2 = None
        self.newK = None
        self.in_size = None  # (w,h)
        self.out_wh = None   # (w,h)

        self._stop = False
        self._thread = threading.Thread(target=self._run, daemon=True)

    def start(self):
        self._thread.start()

    def stop(self):
        self._stop = True
        self._thread.join(timeout=1.0)

    def _open_cap(self) -> cv2.VideoCapture:
        # Windows: use DirectShow, device should be an integer index
        try:
            device_idx = int(self.device)
        except ValueError:
            raise RuntimeError(f"{self.name}: device must be an integer (0, 1, 2, ...), got {self.device}")
        cap = cv2.VideoCapture(device_idx, cv2.CAP_DSHOW)
        
        if self.mjpg:
            cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))
        if self.capture_w:
            cap.set(cv2.CAP_PROP_FRAME_WIDTH, int(self.capture_w))
        if self.capture_h:
            cap.set(cv2.CAP_PROP_FRAME_HEIGHT, int(self.capture_h))
        if self.capture_fps:
            cap.set(cv2.CAP_PROP_FPS, int(self.capture_fps))
        
        # Minimize latency by reducing buffer size (same as capture.py)
        try:
            cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        except Exception:
            pass
        
        return cap

    def _init_undistort(self, frame_w: int, frame_h: int):
        self.in_size = (frame_w, frame_h)

        # Choose output size:
        # - if user asked out_size -> use it
        # - else use yaml_size if present
        # - else match input
        if self.out_size is not None:
            self.out_wh = self.out_size
        elif self.yaml_size is not None:
            self.out_wh = self.yaml_size
        else:
            self.out_wh = (frame_w, frame_h)

        ow, oh = self.out_wh

        if self.model == "fisheye":
            # balance in [0..1] : 0 = crop to valid pixels, 1 = keep all (more black borders)
            self.newK = cv2.fisheye.estimateNewCameraMatrixForUndistortRectify(
                self.K, self.D.reshape(-1, 1), (frame_w, frame_h), np.eye(3), balance=self.balance, new_size=(ow, oh)
            )
            self.map1, self.map2 = cv2.fisheye.initUndistortRectifyMap(
                self.K, self.D.reshape(-1, 1), np.eye(3), self.newK, (ow, oh), cv2.CV_16SC2
            )
        else:
            # Standard pinhole model
            self.newK, _roi = cv2.getOptimalNewCameraMatrix(self.K, self.D, (frame_w, frame_h), 0.0, (ow, oh))
            self.map1, self.map2 = cv2.initUndistortRectifyMap(
                self.K, self.D, None, self.newK, (ow, oh), cv2.CV_16SC2
            )

    def _undistort(self, frame_bgr: np.ndarray) -> np.ndarray:
        if self.map1 is None or self.map2 is None:
            h, w = frame_bgr.shape[:2]
            self._init_undistort(w, h)

        out = cv2.remap(frame_bgr, self.map1, self.map2, interpolation=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)

        if self.rotate_code is not None:
            out = cv2.rotate(out, self.rotate_code)

        return out

    def _run(self):
        cap = self._open_cap()
        backoff = 0.2

        # FPS control (same as capture.py)
        target_fps = self.capture_fps or 30
        period = 1.0 / max(1, target_fps)
        next_t = time.time()

        # Initialize FPS tracking
        self.fps_start_time = time.time()
        self.frame_count = 0

        while not self._stop:
            try:
                if not cap.isOpened():
                    cap.release()
                    time.sleep(backoff)
                    cap = self._open_cap()
                    continue

                # Measure capture time
                t0 = time.time()
                ok, frame = cap.read()
                frame = cv2.rotate(frame, cv2.ROTATE_180) if ok and frame is not None else None
                capture_time = time.time() - t0

                if not ok or frame is None:
                    raise RuntimeError("Failed to read frame")

                # Measure undistort time
                t1 = time.time()
                und = self._undistort(frame)
                undistort_time = time.time() - t1

                # Store timing info for debugging
                total_time = time.time() - t0

                with self.lock:
                    self.last_frame_bgr = und
                    self.last_ok = True
                    self.last_err = ""
                    self.last_ts = time.time()
                    # Store timing data
                    self.capture_time = capture_time
                    self.undistort_time = undistort_time
                    self.total_time = total_time

                    # Update FPS calculation every 30 frames
                    self.frame_count += 1
                    if self.frame_count % 30 == 0:
                        elapsed = time.time() - self.fps_start_time
                        if elapsed > 0:
                            self.actual_fps = int(30.0 / elapsed)
                        self.fps_start_time = time.time()

                # FPS control: sleep to maintain target frame rate
                next_t += period
                sleep = next_t - time.time()
                if sleep > 0:
                    time.sleep(sleep)
                else:
                    next_t = time.time()

            except Exception as e:
                with self.lock:
                    self.last_ok = False
                    self.last_err = str(e)
                time.sleep(backoff)

        cap.release()

    def get_jpeg(self, jpeg_quality: int = 80) -> Optional[bytes]:
        with self.lock:
            frame = None if self.last_frame_bgr is None else self.last_frame_bgr.copy()
        if frame is None:
            return None
        ok, buf = cv2.imencode(".jpg", frame, [int(cv2.IMWRITE_JPEG_QUALITY), int(jpeg_quality)])
        return buf.tobytes() if ok else None

    def status(self) -> Dict[str, Any]:
        with self.lock:
            ts = self.last_ts
            ok = self.last_ok
            err = self.last_err
            cap_time = self.capture_time
            undist_time = self.undistort_time
            enc_time = self.encode_time
            tot_time = self.total_time
            actual_fps = self.actual_fps
        age = (time.time() - ts) if ts > 0 else None
        return {
            "device": self.device,
            "intrinsics": self.intrinsics_path,
            "model": self.model,
            "ok": ok,
            "error": err,
            "age_sec": age,
            "in_size": list(self.in_size) if self.in_size else None,
            "out_size": list(self.out_wh) if self.out_wh else None,
            "balance": self.balance,
            "actual_fps": actual_fps,
            "timing": {
                "capture_ms": round(cap_time * 1000, 2),
                "undistort_ms": round(undist_time * 1000, 2),
                "encode_ms": round(enc_time * 1000, 2),
                "total_ms": round(tot_time * 1000, 2),
            }
        }

# Camera processing functions
def now_stamp() -> str:
    """Return a simple timestamp string for file naming."""
    return time.strftime("%Y%m%d_%H%M%S")

def detect_cameras(max_devices: int = 5) -> List[Tuple[int, str]]:
    """Detect all available cameras and return their device indices and names.

    Returns list of (device_index, device_name) tuples.
    """
    detected_cameras = []
    
    print("🔍 Detecting cameras...")
    
    for i in range(max_devices):
        try:
            if platform.system() == "Windows":
                cap = cv2.VideoCapture(i, cv2.CAP_DSHOW)
            else:
                cap = cv2.VideoCapture(i, cv2.CAP_V4L2)
            
            if cap.isOpened():
                # Try to get a frame to verify the camera is working
                ret, frame = cap.read()
                if ret and frame is not None:
                    # Get camera name/info
                    backend_name = cap.getBackendName()
                    w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
                    h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))

                    detected_cameras.append((i, f"Camera {i} ({w}x{h})"))
                    print(f"  ✓ Found camera at device {i}: {backend_name} ({w}x{h})")

                cap.release()
        except Exception:
            pass
    
    return detected_cameras

def match_exposure(source: np.ndarray, reference: np.ndarray) -> np.ndarray:
    """Match mean/std of source to reference channel-wise."""
    result = np.zeros_like(source, dtype=np.float32)
    for c in range(3):
        src_mean, src_std = source[:, :, c].mean(), source[:, :, c].std()
        ref_mean, ref_std = reference[:, :, c].mean(), reference[:, :, c].std()
        result[:, :, c] = (source[:, :, c] - src_mean) * (ref_std / (src_std + 1e-6)) + ref_mean
    return np.clip(result, 0, 255).astype(np.uint8)

def build_laplacian_pyramid(img: np.ndarray, levels: int) -> list:
    pyramid = [img.astype(np.float32)]
    for _ in range(levels - 1):
        img = cv2.pyrDown(img)
        pyramid.append(img.astype(np.float32))
    return pyramid

def laplacian_pyramid_blend(top: np.ndarray, bottom: np.ndarray, levels: int = 4) -> np.ndarray:
    """Blend two same-size images 50/50 vertically using Laplacian pyramid blending."""
    h, w = top.shape[:2]
    mask = np.zeros((h, w), dtype=np.float32)
    mask[: h // 2] = 1.0
    mask[h // 2 :] = np.linspace(1.0, 0.0, h - h // 2, dtype=np.float32)[:, None]
    mask = np.stack([mask] * 3, axis=-1)

    gp_top = build_laplacian_pyramid(top, levels)
    gp_bot = build_laplacian_pyramid(bottom, levels)

    lp_top, lp_bot = [], []
    for i in range(levels - 1):
        up_top = cv2.pyrUp(gp_top[i + 1], dstsize=(gp_top[i].shape[1], gp_top[i].shape[0]))
        up_bot = cv2.pyrUp(gp_bot[i + 1], dstsize=(gp_bot[i].shape[1], gp_bot[i].shape[0]))
        lp_top.append(gp_top[i] - up_top)
        lp_bot.append(gp_bot[i] - up_bot)
    lp_top.append(gp_top[-1])
    lp_bot.append(gp_bot[-1])

    gp_mask = build_laplacian_pyramid(mask, levels)

    blended = [lt * gm + lb * (1.0 - gm) for lt, lb, gm in zip(lp_top, lp_bot, gp_mask)]

    result = blended[-1]
    for i in range(levels - 2, -1, -1):
        result = cv2.pyrUp(result, dstsize=(blended[i].shape[1], blended[i].shape[0]))
        result += blended[i]

    return np.clip(result, 0, 255).astype(np.uint8)

def find_overlap_offset(
    top_frame: np.ndarray,
    bottom_frame: np.ndarray,
    overlap_height: int,
    max_offset_x: int = 20,
) -> tuple[int, int]:
    """
    Use ORB keypoint matching within the overlap regions to find the (dx, dy)
    translation offset between the two frames.

    top_frame/bottom_frame are already resized to target dimensions.
    Returns (dx, dy): how much to shift bottom relative to top.
    Falls back to (0, 0) if not enough matches are found.
    """
    # Extract overlap strips
    top_strip = top_frame[-overlap_height:]      # bottom rows of top camera
    bot_strip = bottom_frame[:overlap_height]    # top rows of bottom camera

    gray_top = cv2.cvtColor(top_strip, cv2.COLOR_BGR2GRAY)
    gray_bot = cv2.cvtColor(bot_strip, cv2.COLOR_BGR2GRAY)

    orb = cv2.ORB_create(nfeatures=500)
    kp_top, des_top = orb.detectAndCompute(gray_top, None)
    kp_bot, des_bot = orb.detectAndCompute(gray_bot, None)

    if des_top is None or des_bot is None or len(kp_top) < 4 or len(kp_bot) < 4:
        return (0, 0)

    matcher = cv2.BFMatcher(cv2.NORM_HAMMING, crossCheck=True)
    matches = matcher.match(des_top, des_bot)

    if len(matches) < 4:
        return (0, 0)

    # Sort by distance and keep best 50%
    matches = sorted(matches, key=lambda m: m.distance)
    good = matches[: max(4, len(matches) // 2)]

    # Compute median translation from matched point pairs
    # dy is relative between the two strips — not meaningful as absolute shift,
    # but dx tells us horizontal misalignment
    dx_vals = []
    for m in good:
        pt_top = kp_top[m.queryIdx].pt   # (x, y) in top strip
        pt_bot = kp_bot[m.trainIdx].pt   # (x, y) in bot strip
        dx_vals.append(pt_bot[0] - pt_top[0])

    dx = int(np.median(dx_vals))
    dx = int(np.clip(dx, -max_offset_x, max_offset_x))  # sanity clamp

    return (dx, 0)

def find_vertical_seam(
    top_frame: np.ndarray,
    bottom_frame: np.ndarray,
    min_overlap: int = 60,
    max_overlap: int = 240,
    step: int = 10,
) -> tuple[int, int]:
    """
    Search for the best vertical alignment between top and bottom frames by
    sliding the bottom frame upward over the top frame and finding the offset
    with the highest ORB feature match quality.

    Returns (best_overlap_rows, best_dx):
        best_overlap_rows: how many rows of the bottom frame overlap with the top
        best_dx: horizontal shift to apply to the bottom frame before compositing
    """
    h, w = top_frame.shape[:2]

    orb = cv2.ORB_create(nfeatures=400)
    best_score = -1
    best_overlap = min_overlap
    best_dx = 0

    for overlap in range(min_overlap, min(max_overlap, h), step):
        top_strip = top_frame[-overlap:]
        bot_strip = bottom_frame[:overlap]

        gray_top = cv2.cvtColor(top_strip, cv2.COLOR_BGR2GRAY)
        gray_bot = cv2.cvtColor(bot_strip, cv2.COLOR_BGR2GRAY)

        kp_t, des_t = orb.detectAndCompute(gray_top, None)
        kp_b, des_b = orb.detectAndCompute(gray_bot, None)

        if des_t is None or des_b is None or len(kp_t) < 4 or len(kp_b) < 4:
            continue

        matcher = cv2.BFMatcher(cv2.NORM_HAMMING, crossCheck=True)
        matches = matcher.match(des_t, des_b)
        if len(matches) < 4:
            continue

        matches = sorted(matches, key=lambda m: m.distance)
        good = matches[: max(4, len(matches) // 2)]

        # Score = number of good matches, weighted by inverse distance
        score = sum(1.0 / (m.distance + 1e-6) for m in good)

        if score > best_score:
            best_score = score
            best_overlap = overlap
            dx_vals = [
                kp_b[m.trainIdx].pt[0] - kp_t[m.queryIdx].pt[0]
                for m in good
            ]
            best_dx = int(np.clip(np.median(dx_vals), -30, 30))

    return best_overlap, best_dx

def stitch_cameras(
    frame1: np.ndarray,
    frame2: np.ndarray,
    topCam: str,
    cam1_name: str,
    cam2_name: str,
    overlap_height: int = 120,
    cached_state: list | None = None,  # pass a mutable [dx] list to cache offset across frames
) -> np.ndarray:
    """
    Stitch two camera frames vertically.
    - overlap_height: minimum 1/4 of target height (120px for 480px).
    - Feature matching in overlap region corrects horizontal misalignment.
    - Laplacian pyramid blending hides the seam.
    """
    target_width = 640
    target_height = 480

    if topCam == cam1_name:
        top_frame, bottom_frame = frame1, frame2
        top_name, bottom_name = cam1_name, cam2_name
    else:
        top_frame, bottom_frame = frame2, frame1
        top_name, bottom_name = cam2_name, cam1_name

    top_resized = cv2.resize(top_frame, (target_width, target_height))
    bot_resized = cv2.resize(bottom_frame, (target_width, target_height))

    # --- Step 1: find or reuse the best overlap + dx ---
    # Recompute every 30 frames; use cache in between for speed
    recompute = True
    if cached_state is not None and len(cached_state) == 3:
        cached_overlap, cached_state, cached_age = cached_state
        if cached_age < 30:
            overlap = cached_overlap
            dx = cached_state
            cached_state[2] += 1
            recompute = False

    if recompute:
        overlap, dx = find_vertical_seam(
            top_resized, bot_resized,
            min_overlap=overlap_height,
            max_overlap=target_height // 2,
            step=10,
        )
        if cached_state is not None:
            cached_state.clear()
            cached_state.extend([overlap, dx, 0])
    
    # --- Step 2: apply horizontal correction to bottom frame ---
    if dx != 0:
        M = np.float32([[1, 0, -dx], [0, 1, 0]])
        bot_resized = cv2.warpAffine(
            bot_resized, M, (target_width, target_height),
            borderMode=cv2.BORDER_REPLICATE,
        )

    # --- Step 3: match exposure in overlap zone ---
    top_strip = top_resized[-overlap:]
    bot_strip = bot_resized[:overlap]
    bot_strip = match_exposure(bot_strip, top_strip)

    # --- Step 4: Laplacian pyramid blend the overlap strip ---
    blended_strip = laplacian_pyramid_blend(top_strip, bot_strip, levels=4)

    # --- Step 5: assemble — output height = (480 - overlap) + overlap + (480 - overlap)
    #             = 960 - overlap, so more overlap = shorter, less segmented output ---
    top_body    = top_resized[: target_height - overlap]
    bottom_body = bot_resized[overlap:]

    stitched = cv2.vconcat([top_body, blended_strip, bottom_body])

    # --- Step 6: label ---
    out_h = stitched.shape[0]
    font = cv2.FONT_HERSHEY_SIMPLEX
    cv2.putText(stitched, f"Top: {top_name}",    (10, 30),          font, 0.9, (255, 0, 255), 2)
    cv2.putText(stitched, f"Bot: {bottom_name}", (10, out_h // 2 + 30), font, 0.9, (255, 0, 255), 2)

    return stitched


def find_horizontal_offset(
    left_frame: np.ndarray,
    right_frame: np.ndarray,
    overlap_width: int,
    max_offset_y: int = 20,
) -> tuple[int, int]:
    """
    Use ORB keypoint matching within the overlap regions to find the (dx, dy)
    translation offset between the two frames.

    left_frame/right_frame are already resized to target dimensions.
    Returns (dx, dy): how much to shift right relative to left.
    Falls back to (0, 0) if not enough matches are found.
    """
    # Extract overlap strips
    left_strip = left_frame[:, -overlap_width:]      # right columns of left camera
    right_strip = right_frame[:, :overlap_width]     # left columns of right camera

    gray_left = cv2.cvtColor(left_strip, cv2.COLOR_BGR2GRAY)
    gray_right = cv2.cvtColor(right_strip, cv2.COLOR_BGR2GRAY)

    orb = cv2.ORB_create(nfeatures=500)
    kp_left, des_left = orb.detectAndCompute(gray_left, None)
    kp_right, des_right = orb.detectAndCompute(gray_right, None)

    if des_left is None or des_right is None or len(kp_left) < 4 or len(kp_right) < 4:
        return (0, 0)

    matcher = cv2.BFMatcher(cv2.NORM_HAMMING, crossCheck=True)
    matches = matcher.match(des_left, des_right)

    if len(matches) < 4:
        return (0, 0)

    # Sort by distance and keep best 50%
    matches = sorted(matches, key=lambda m: m.distance)
    good = matches[: max(4, len(matches) // 2)]

    # Compute median translation from matched point pairs
    # dx is relative between the two strips — not meaningful as absolute shift,
    # but dy tells us vertical misalignment
    dy_vals = []
    for m in good:
        pt_left = kp_left[m.queryIdx].pt   # (x, y) in left strip
        pt_right = kp_right[m.trainIdx].pt # (x, y) in right strip
        dy_vals.append(pt_right[1] - pt_left[1])

    dy = int(np.median(dy_vals))
    dy = int(np.clip(dy, -max_offset_y, max_offset_y))  # sanity clamp

    return (0, dy)


def find_horizontal_seam(
    left_frame: np.ndarray,
    right_frame: np.ndarray,
    min_overlap: int = 60,
    max_overlap: int = 240,
    step: int = 10,
) -> tuple[int, int]:
    """
    Search for the best horizontal alignment between left and right frames by
    sliding the right frame leftward over the left frame and finding the offset
    with the highest ORB feature match quality.

    Returns (best_overlap_cols, best_dy):
        best_overlap_cols: how many columns of the right frame overlap with the left
        best_dy: vertical shift to apply to the right frame before compositing
    """
    h, w = left_frame.shape[:2]

    orb = cv2.ORB_create(nfeatures=400)
    best_score = -1
    best_overlap = min_overlap
    best_dy = 0

    for overlap in range(min_overlap, min(max_overlap, w), step):
        left_strip = left_frame[:, -overlap:]
        right_strip = right_frame[:, :overlap]

        gray_left = cv2.cvtColor(left_strip, cv2.COLOR_BGR2GRAY)
        gray_right = cv2.cvtColor(right_strip, cv2.COLOR_BGR2GRAY)

        kp_l, des_l = orb.detectAndCompute(gray_left, None)
        kp_r, des_r = orb.detectAndCompute(gray_right, None)

        if des_l is None or des_r is None or len(kp_l) < 4 or len(kp_r) < 4:
            continue

        matcher = cv2.BFMatcher(cv2.NORM_HAMMING, crossCheck=True)
        matches = matcher.match(des_l, des_r)
        if len(matches) < 4:
            continue

        matches = sorted(matches, key=lambda m: m.distance)
        good = matches[: max(4, len(matches) // 2)]

        # Score = number of good matches, weighted by inverse distance
        score = sum(1.0 / (m.distance + 1e-6) for m in good)

        if score > best_score:
            best_score = score
            best_overlap = overlap
            dy_vals = [
                kp_r[m.trainIdx].pt[1] - kp_l[m.queryIdx].pt[1]
                for m in good
            ]
            best_dy = int(np.clip(np.median(dy_vals), -30, 30))

    return best_overlap, best_dy


def stitch_horizontal(
    frame1: np.ndarray,
    frame2: np.ndarray,
    leftCam: str,
    cam1_name: str,
    cam2_name: str,
    overlap_width: int = 120,
    cached_state: list | None = None,  # pass a mutable [dy] list to cache offset across frames
) -> np.ndarray:
    """
    Stitch two camera frames horizontally.
    - overlap_width: minimum 1/4 of target width (120px for 480px).
    - Feature matching in overlap region corrects vertical misalignment.
    - Laplacian pyramid blending hides the seam.
    """
    h1, w1 = frame1.shape[:2]
    h2, w2 = frame2.shape[:2]
    target_width = w1  # assume both are same width
    target_height = h1

    if leftCam == cam1_name:
        left_frame, right_frame = frame1, frame2
        left_name, right_name = cam1_name, cam2_name
    else:
        left_frame, right_frame = frame2, frame1
        left_name, right_name = cam2_name, cam1_name

    # Assume frames are already resized to same size
    left_resized = left_frame
    right_resized = right_frame

    # --- Step 1: find or reuse the best overlap + dy ---
    # Recompute every 30 frames; use cache in between for speed
    recompute = True
    if cached_state is not None and len(cached_state) == 3:
        cached_overlap, cached_state, cached_age = cached_state
        if cached_age < 30:
            overlap = cached_overlap
            dy = cached_state
            cached_state[2] += 1
            recompute = False

    if recompute:
        overlap, dy = find_horizontal_seam(
            left_resized, right_resized,
            min_overlap=overlap_width,
            max_overlap=target_width // 2,
            step=10,
        )
        if cached_state is not None:
            cached_state.clear()
            cached_state.extend([overlap, dy, 0])

    # --- Step 2: apply vertical correction to right frame ---
    if dy != 0:
        M = np.float32([[1, 0, 0], [0, 1, -dy]])
        right_resized = cv2.warpAffine(
            right_resized, M, (target_width, target_height),
            borderMode=cv2.BORDER_REPLICATE,
        )

    # --- Step 3: match exposure in overlap zone ---
    left_strip = left_resized[:, -overlap:]
    right_strip = right_resized[:, :overlap]
    right_strip = match_exposure(right_strip, left_strip)

    # --- Step 4: Laplacian pyramid blend the overlap strip ---
    blended_strip = laplacian_pyramid_blend(left_strip, right_strip, levels=4)

    # --- Step 5: assemble — output width = (w - overlap) + overlap + (w - overlap)
    #             = 2*w - overlap ---
    left_body    = left_resized[:, : target_width - overlap]
    right_body = right_resized[:, overlap:]

    stitched = cv2.hconcat([left_body, blended_strip, right_body])

    # --- Step 6: label ---
    out_w = stitched.shape[1]
    font = cv2.FONT_HERSHEY_SIMPLEX
    cv2.putText(stitched, f"Left: {left_name}",    (10, 30),          font, 0.9, (255, 0, 255), 2)
    cv2.putText(stitched, f"Right: {right_name}", (out_w // 2 + 10, 30), font, 0.9, (255, 0, 255), 2)

    return stitched


def build_panoramic_front(
    front_frame: np.ndarray,
    left_frame: np.ndarray,
    right_frame: np.ndarray,
    target_size: Tuple[int, int] = (1280, 480),
    label: bool = True,
) -> np.ndarray:
    """Compose left/front/right frames into a single front panoramic view with seamless stitching."""
    out_w, out_h = target_size
    part_w = out_w // 3

    def resize_or_blank(frame: Optional[np.ndarray]) -> np.ndarray:
        if frame is None:
            return np.zeros((out_h, part_w, 3), dtype=np.uint8)
        return cv2.resize(frame, (part_w, out_h), interpolation=cv2.INTER_LINEAR)

    left_resized = resize_or_blank(left_frame)
    front_resized = resize_or_blank(front_frame)
    right_resized = resize_or_blank(right_frame)

    # Stitch left and front seamlessly
    left_front_stitched = stitch_horizontal(
        left_resized, front_resized, "left", "left", "front", overlap_width=60
    )

    # Stitch the result with right seamlessly
    panorama = stitch_horizontal(
        left_front_stitched, right_resized, "left", "left_front", "right", overlap_width=60
    )

    if label:
        font = cv2.FONT_HERSHEY_SIMPLEX
        # Approximate positions
        cv2.putText(panorama, "LEFT", (10, 30), font, 1.0, (255, 255, 255), 2, cv2.LINE_AA)
        cv2.putText(panorama, "FRONT", (part_w + 10, 30), font, 1.0, (255, 255, 255), 2, cv2.LINE_AA)
        cv2.putText(panorama, "RIGHT", (2 * part_w + 10, 30), font, 1.0, (255, 255, 255), 2, cv2.LINE_AA)

    return panorama


def bev_warp(
    frame: np.ndarray,
    src_points: np.ndarray,
    dst_size: Tuple[int, int] = (640, 480),
    dst_points: Optional[np.ndarray] = None,
) -> np.ndarray:
    """Warp the frame into a bird's-eye view using a homography.

    src_points should be four source points in the input image order:
    top-left, top-right, bottom-right, bottom-left.
    dst_points defaults to the four corners of the output rectangle.
    """
    if frame is None:
        raise ValueError("frame must be a valid image")

    src = np.array(src_points, dtype=np.float32)
    if src.shape != (4, 2):
        raise ValueError("src_points must be shape (4, 2)")

    if dst_points is None:
        w, h = dst_size
        dst = np.array(
            [[0, 0], [w - 1, 0], [w - 1, h - 1], [0, h - 1]],
            dtype=np.float32,
        )
    else:
        dst = np.array(dst_points, dtype=np.float32)
        if dst.shape != (4, 2):
            raise ValueError("dst_points must be shape (4, 2)")

    matrix = cv2.getPerspectiveTransform(src, dst)
    warped = cv2.warpPerspective(
        frame,
        matrix,
        dst_size,
        flags=cv2.INTER_LINEAR,
        borderMode=cv2.BORDER_CONSTANT,
        borderValue=(0, 0, 0),
    )
    return warped


def expand(p: str) -> str:
    return os.path.abspath(os.path.expanduser(p))

def load_yaml(path: str) -> dict:
    with open(path, "r") as f:
        return yaml.safe_load(f)

def as_np_mat(x) -> np.ndarray:
    a = np.array(x, dtype=np.float64)
    return a

def load_intrinsics(path: str) -> Tuple[np.ndarray, np.ndarray, Optional[Tuple[int, int]]]:
    """
    Supports common YAML layouts, e.g.:
      K: [[...],[...],[...]]
      D: [k1,k2,k3,k4]           (fisheye)
      image_size: [w,h]

    Or:
      camera_matrix: {data:[...], rows:3, cols:3}
      dist_coeffs: {data:[...]}
      width: 640
      height: 480

    Or:
      camera_matrix: [[...],[...],[...]]
      distortion_coefficients: [...]
    """
    raw = load_yaml(path) or {}

    # Try multiple keys for K
    K = None
    for key in ("K", "camera_matrix", "cameraMatrix", "intrinsic_matrix", "camera_intrinsics"):
        if key in raw:
            K = raw[key]
            break

    if isinstance(K, dict) and "data" in K:
        K = np.array(K["data"], dtype=np.float64).reshape(3, 3)
    else:
        K = as_np_mat(K) if K is not None else None

    # Try multiple keys for D
    D = None
    for key in ("D", "dist_coeffs", "distCoeffs", "distortion_coefficients", "distortion", "distortion_coeffs"):
        if key in raw:
            D = raw[key]
            break

    if isinstance(D, dict) and "data" in D:
        D = np.array(D["data"], dtype=np.float64).reshape(-1)
    else:
        D = np.array(D, dtype=np.float64).reshape(-1) if D is not None else None

    if K is None or D is None:
        raise ValueError(f"Intrinsics YAML missing K/D: {path}")

    # Image size (optional but recommended)
    size = None
    if "image_size" in raw and isinstance(raw["image_size"], (list, tuple)) and len(raw["image_size"]) == 2:
        w, h = int(raw["image_size"][0]), int(raw["image_size"][1])
        size = (w, h)
    else:
        w = raw.get("width", raw.get("image_width", raw.get("w")))
        h = raw.get("height", raw.get("image_height", raw.get("h")))
        if w is not None and h is not None:
            size = (int(w), int(h))

    return K, D, size
