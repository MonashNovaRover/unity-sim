# probe_tcp.py
import socket, time

HOST, PORT = "127.0.0.1", 5000
sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.connect((HOST, PORT))

for i in range(10):
    payload = f"ping{i:04d}".encode()
    t0 = time.perf_counter()
    sock.sendall(payload)
    sock.recv(64)
    rtt = (time.perf_counter() - t0) * 1000
    print(f"seq={i}  rtt={rtt:.3f} ms")
    time.sleep(1)

sock.close()