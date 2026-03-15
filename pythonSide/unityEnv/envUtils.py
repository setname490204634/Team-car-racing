import os
import socket
import time
import re

def wait_for_port(host: str, port: int, timeout=20):
    """Wait until a TCP port is open."""
    start = time.time()
    while time.time() - start < timeout:
        try:
            with socket.create_connection((host, port), timeout=1):
                print(f"Unity is ready on port {port}")
                return True
        except (ConnectionRefusedError, OSError):
            time.sleep(0.5)
    print(f"Timeout: Unity did not open port {port} in {timeout} seconds.")
    return False

def get_os_assigned_port():
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    return s, port

def get_next_env_folder(log_dir):
    os.makedirs(log_dir, exist_ok=True)

    env_numbers = []

    for name in os.listdir(log_dir):
        match = re.match(r"env_(\d+)", name)
        if match:
            env_numbers.append(int(match.group(1)))

    next_number = max(env_numbers) + 1 if env_numbers else 1
    next_env_dir = os.path.join(log_dir, f"env_{next_number}")
    os.makedirs(next_env_dir, exist_ok=True)
    return next_env_dir