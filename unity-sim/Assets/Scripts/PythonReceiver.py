# netstat -ano | findstr :5000

import socket, struct, cv2
import numpy as np

# Set up TCP server socket same as Unity's FrameTransmitter
server = socket.socket()
server.bind(("127.0.0.1", 5000))
server.listen(1)
print("Waiting on Unity TCP connection...")
conn, _ = server.accept()

# Receive camera frames from Unity
def receive_frame(conn):
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
    return cv2.imdecode(arr, cv2.IMREAD_COLOR)

while True:
    # print("Starting to receive frame...")
    frame = receive_frame(conn)

    cv2.imshow("Cam0", frame)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cv2.destroyAllWindows()
conn.close()