# netstat -ano | findstr :5000
# | 1 byte camId | 4 bytes frame length | N bytes JPG data |

import socket, struct, cv2
import numpy as np
import argparse

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
    )

    args = parser.parse_args()

    # Single camera feed
    if args.mode == 0:
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
    elif args.mode == 1:
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

if __name__ == "__main__":
    main()