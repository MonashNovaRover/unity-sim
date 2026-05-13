# netstat -ano | findstr :5000
# | 1 byte camId | 4 bytes frame length | N bytes JPG data |
# python .\Assets\Scripts\Python\PythonReceiver.py --mode 2

import socket, struct, cv2, time
import numpy as np
import argparse
from Cameras import build_panoramic_feather, bev_warp, build_vertical_feather

# Receive camera frames from Unity
def receive_frame(conn):
    # Read 1 byte for camera ID
    # Convert byte to int
    cam_id = conn.recv(1)
    cam_id = cam_id[0]

    # Read 4 bytes for frame package as specified in transmitter
    # Interpret 4 bytes as little-endian unsigned integer to match C#'s BitConverter
    raw_len = conn.recv(4)
    length = struct.unpack('<I', raw_len)[0]
    data = b''

    # Keep reading until TCP delivers entire frame data matching "length"
    while len(data) < length:
        data += conn.recv(length - len(data))

    # Wrap raw bytes into NP array
    # Decode array as BGR image
    arr = np.frombuffer(data, dtype=np.uint8)
    return cam_id, cv2.imdecode(arr, cv2.IMREAD_COLOR)

_fps_last_time = {}
_fps_values = {}

def stamp_fps(frame, window_name):
    """Compute per-window FPS and draw it onto the frame in-place."""
    now = time.perf_counter()
    if window_name in _fps_last_time:
        delta = now - _fps_last_time[window_name]
        # Smooth with exponential moving average (alpha=0.1)
        prev = _fps_values.get(window_name, 0.0)
        fps = 0.9 * prev + 0.1 * (1.0 / delta if delta > 0 else prev)
    else:
        fps = 0.0
    _fps_last_time[window_name] = now
    _fps_values[window_name] = fps

    label = f"FPS: {fps:.1f}"
    org = (10, 25)
    font = cv2.FONT_HERSHEY_SIMPLEX
    scale, thickness = 0.7, 2
    # Dark outline for readability on any background
    cv2.putText(frame, label, org, font, scale, (0, 0, 0), thickness + 2, cv2.LINE_AA)
    cv2.putText(frame, label, org, font, scale, (0, 255, 0), thickness,   cv2.LINE_AA)
    return frame

def main():
    parser = argparse.ArgumentParser(description="Running Python TCP server to play frames from Unity.")
    parser.add_argument(
        "--mode", 
        type=int,
        default=0
    )

    args = parser.parse_args()

    # ------------------------------------------------------------------ mode 0
    if args.mode == 0:      # Single camera feed
        # Set up TCP server socket same as Unity's FrameTransmitter
        server = socket.socket()
        server.bind(("127.0.0.1", 5000))
        server.listen(1)
        print("Waiting on Unity TCP connection...")
        conn, _ = server.accept()
        print("TCP established")

        while True:
            # print("Starting to receive frame...")
            frame = receive_frame(conn)

            cv2.imshow("Cam0", frame)
            
            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

        cv2.destroyAllWindows()
        conn.close()

    # ------------------------------------------------------------------ mode 1    
    elif args.mode == 1:        # 4 cameras feed
        server = socket.socket()
        server.bind(("127.0.0.1", 5000))
        server.listen(1)
        print("Waiting on Unity TCP connection...")
        conn, _ = server.accept()
        print("TCP established")

        frames = {0: None, 1: None, 2: None, 3: None}
        while True:
            cam_id, frame = receive_frame(conn)
            frames[cam_id] = frame
            for cam_id, frame in frames.items():
                if frame is not None:
                    win = f"Cam{cam_id}"
                    stamp_fps(frame, win)                    
                    cv2.imshow(f"Cam{cam_id}", frame)

            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

    # ------------------------------------------------------------------ mode 2
    elif args.mode == 2:    # 4-camera-panoramic with diagonal camera placement
    # [cam0, cam1, cam2, cam3] = [front-left, front-right, back-right, back-left]

        def print_frame_coordinates(event, x, y, flags, param):
            if event == cv2.EVENT_LBUTTONDOWN:
                window_name = param if isinstance(param, str) else "Cam0"
                print(f"[{window_name}] clicked at frame coords: ({x}, {y})")

        server = socket.socket()
        server.bind(("127.0.0.1", 5000))
        server.listen(1)
        print("Waiting on Unity TCP connection...")
        conn, _ = server.accept()
        print("TCP established")

        frames = {0: None, 1: None, 2: None, 3: None}
        bevs = {0: None, 1: None, 2: None, 3: None}

        # # Points for front two at Y-rot = 45 deg angle cam offset
        # # Need overlap of 55
        # src_points0 = np.array(
        #     [
        #         [230, 180],  # top-left
        #         [640, 0],  # top-right
        #         [640, 480],  # bottom-right
        #         [230, 300],   # bottom-left
        #     ],
        #     dtype=np.float32,
        # )
        # src_points1 = np.array(
        #     [
        #         [0, 0],  # top-left
        #         [410, 180],  # top-right
        #         [410, 300],  # bottom-right
        #         [0, 480],   # bottom-left
        #     ],
        #     dtype=np.float32,
        # )

        # Points for front two at Y-rot = 30 deg angle cam offset
        # Need overlap of 160
        src_points0 = np.array(
            [
                [130, 170],  # top-left
                [640, 0],  # top-right
                [640, 480],  # bottom-right
                [130, 310],   # bottom-left
            ],
            dtype=np.float32,
        )
        src_points1 = np.array(
            [
                [0, 0],  # top-left
                [510, 170],  # top-right
                [510, 310],  # bottom-right
                [0, 480],   # bottom-left
            ],
            dtype=np.float32,
        )

        dst_size = (640, 480)

        outsize = (1280, 480)
        seam_overlap = 160

        while True:
            cam_id, frame = receive_frame(conn)
            frames[cam_id] = frame
            if all(f is not None for f in frames.values()):
                for cam_id, frame in frames.items():
                    cv2.imshow(f"Cam{cam_id}", frame)
                    if cam_id in [0, 2]:
                        cv2.polylines(frame, [src_points0.astype(int)], isClosed=True, color=(0, 255, 255), thickness=1)
                        # for point in src_points0:
                        #     cv2.circle(frame, tuple(point.astype(int)), 5, (0, 0, 255), -1)

                        bevs[cam_id] = bev_warp(frame, src_points0, dst_size=dst_size)

                    elif cam_id in [1, 3]:
                        cv2.polylines(frame, [src_points1.astype(int)], isClosed=True, color=(0, 255, 255), thickness=1)
                        # for point in src_points1:
                        #     cv2.circle(frame, tuple(point.astype(int)), 5, (0, 0, 255), -1)

                        bevs[cam_id] = bev_warp(frame, src_points1, dst_size=dst_size)
                    
                    # cv2.imshow(f"Cam{cam_id} BEV", bevs[cam_id])
    
                front = build_panoramic_feather(
                    left_frame = bevs[0],
                    right_frame = bevs[1],
                    target_size = outsize,
                    overlap = seam_overlap
                )

                back = build_panoramic_feather(
                    left_frame = bevs[2],
                    right_frame = bevs[3],
                    target_size = outsize,
                    overlap = seam_overlap
                )

                stamp_fps(front, "Front")
                stamp_fps(back,  "Back")
                cv2.imshow("Front", front)
                cv2.imshow("Back", back)

            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

        cv2.destroyAllWindows()
        conn.close()

    # ------------------------------------------------------------------ mode 3
    elif args.mode == 3:    # 4-camera vertical stitch
    # [cam0, cam1, cam2, cam3] = [front-bottom, front-top, back-top, back-bottom]

        def print_frame_coordinates(event, x, y, flags, param):
            if event == cv2.EVENT_LBUTTONDOWN:
                window_name = param if isinstance(param, str) else "Cam0"
                print(f"[{window_name}] clicked at frame coords: ({x}, {y})")

        server = socket.socket()
        server.bind(("127.0.0.1", 5000))
        server.listen(1)
        print("Waiting on Unity TCP connection...")
        conn, _ = server.accept()
        print("TCP established")

        frames = {0: None, 1: None, 2: None, 3: None}

        while True:
            cam_id, frame = receive_frame(conn)
            frames[cam_id] = frame
            if all(f is not None for f in frames.values()):
                front = build_vertical_feather(
                    top_frame = frames[1],
                    bot_frame = frames[0],
                    target_size = (640, 960),
                    overlap = 200
                )

                back = build_vertical_feather(
                    top_frame = frames[3],
                    bot_frame = frames[2],
                    target_size = (640, 960),
                    overlap = 200
                )

                stamp_fps(front, "Front")
                stamp_fps(back,  "Back")
                cv2.imshow("Front", front)
                cv2.imshow("Back", back)
        
            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

        cv2.destroyAllWindows()
        conn.close()

    # ------------------------------------------------------------------ mode 4
    elif args.mode == 4:    # Single floating camera
        server = socket.socket()
        server.bind(("127.0.0.1", 5000))
        server.listen(1)
        print("Waiting on Unity TCP connection...")
        conn, _ = server.accept()
        print("TCP established")     

        # frames = {0: None, 1: None, 2: None, 3: None, 4: None}  

        while True:
            cam_id, frame = receive_frame(conn)
            if frame is not None and cam_id == 0:
                stamp_fps(frame, "Floating Camera")
                cv2.imshow(f"Floating Camera", frame)

            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

        cv2.destroyAllWindows()
        conn.close()                

    # ------------------------------------------------------------------ mode 5
    elif args.mode == 5:    # Testing BEV warp
        def print_frame_coordinates(event, x, y, flags, param):
            if event == cv2.EVENT_LBUTTONDOWN:
                window_name = param if isinstance(param, str) else "Cam0"
                print(f"[{window_name}] clicked at frame coords: ({x}, {y})")

        server = socket.socket()
        server.bind(("127.0.0.1", 5000))
        server.listen(1)
        print("Waiting on Unity TCP connection...")
        conn, _ = server.accept()
        print("TCP established")

        # Hardcoded source points for BEV homography. Change these values as needed.
        # src_points = np.array(
        #     [
        #         [100, 260],  # top-left
        #         [540, 260],  # top-right
        #         [620, 480],  # bottom-right
        #         [20, 480],   # bottom-left
        #     ],
        src_points = np.array(
            [
                [210, 130],  # top-left
                [430, 130],  # top-right
                [610, 340],  # bottom-right
                [30, 340],   # bottom-left
            ],
            dtype=np.float32,
        )
        dst_size = (640, 480)

        cv2.namedWindow("Cam0", cv2.WINDOW_NORMAL)
        cv2.setMouseCallback("Cam0", print_frame_coordinates, "Cam0")

        frames = {0: None}
        while True:
            cam_id, frame = receive_frame(conn)
            if cam_id != 0:
                continue
            
            cv2.polylines(frame, [src_points.astype(int)], isClosed=True, color=(0, 255, 255), thickness=1)
            for point in src_points:
                cv2.circle(frame, tuple(point.astype(int)), 5, (0, 0, 255), -1)

            bev = bev_warp(frame, src_points, dst_size=dst_size)

            cv2.imshow("Cam0", frame)
            cv2.imshow("Cam0_BEV", bev)

            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

        cv2.destroyAllWindows()
        conn.close()

if __name__ == "__main__":
    main()