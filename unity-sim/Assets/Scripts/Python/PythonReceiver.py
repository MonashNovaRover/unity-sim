# netstat -ano | findstr :5000
# | 1 byte camId | 4 bytes frame length | N bytes JPG data |
# python .\Assets\Scripts\Python\PythonReceiver.py --mode 2

import socket, struct, cv2
import numpy as np
import argparse
from Cameras import bev_warp, build_panoramic_front

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


def main():
    parser = argparse.ArgumentParser(description="Running Python TCP server to play frames from Unity.")
    parser.add_argument(
        "--mode", 
        type=int,
        default=0,
        help="" \
        "0: (Default) Single camera feed. " \
        "1: 2 cameras feed. " \
        "2: 4 cameras feed. " \
        "3: 4 cameras feed with bird's-eye homography warp. " \
        "4: 4 cameras feed with front panorama + back original. " \
    )

    args = parser.parse_args()

    # Single camera feed
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
    
    # 2 camera feed
    elif args.mode == 1:        # 2 cameras feed
        server = socket.socket()
        server.bind(("127.0.0.1", 5000))
        server.listen(1)
        print("Waiting on Unity TCP connection...")
        conn, _ = server.accept()
        print("TCP established")

        frames = {0: None, 1: None}

        while True:
            cam_id, frame = receive_frame(conn)
            frames[cam_id] = frame
            for cam_id, frame in frames.items():
                if frame is not None:
                    cv2.imshow(f"Cam{cam_id}", frame)

            if cv2.waitKey(1) & 0xFF == ord('q'):
                break
    
    elif args.mode == 2:        # 4 cameras feed
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
                    cv2.imshow(f"Cam{cam_id}", frame)

            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

    elif args.mode == 3:    # 4 cameras feed with BEV warp
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

    elif args.mode == 4:    # 4 cameras front/back panoramic composition
        server = socket.socket()
        server.bind(("127.0.0.1", 5000))
        server.listen(1)
        print("Waiting on Unity TCP connection...")
        conn, _ = server.accept()
        print("TCP established")

        frames = {0: None, 1: None, 2: None, 3: None}
        front_window = "Front_Panoramic"
        back_window = "Back_Original"

        cv2.namedWindow(front_window, cv2.WINDOW_NORMAL)
        cv2.namedWindow(back_window, cv2.WINDOW_NORMAL)
        cv2.resizeWindow(front_window, 1280, 480)
        cv2.resizeWindow(back_window, 640, 480)

        while True:
            cam_id, frame = receive_frame(conn)
            if frame is None or cam_id not in frames:
                continue

            frames[cam_id] = frame

            if any(frames[k] is None for k in frames):
                continue

            front_panorama = build_panoramic_front(
                front_frame=frames[0],
                left_frame=frames[2],
                right_frame=frames[3],
                target_size=(1280, 480),
            )
            back_original = cv2.resize(frames[1], (640, 480), interpolation=cv2.INTER_LINEAR)

            cv2.imshow(front_window, front_panorama)
            cv2.imshow(back_window, back_original)

            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

        cv2.destroyAllWindows()
        conn.close()

if __name__ == "__main__":
    main()