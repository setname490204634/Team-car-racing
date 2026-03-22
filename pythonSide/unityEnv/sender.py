import socket
import struct

HOST = "127.0.0.1"

# Persistent sockets
_control_socket = None
_car_socket = None

def init_control_socket(port: int):
    global _control_socket
    if _control_socket is None:
        _control_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        _control_socket.connect((HOST, port))

def init_car_socket(port: int):
    global _car_socket
    if _car_socket is None:
        _car_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        _car_socket.connect((HOST, port))

def send_command(command_byte: int, value_byte: int, port: int):
    """
    Send a 2-byte command packet to Unity control server.
    """
    global _control_socket
    if _control_socket is None:
        init_control_socket(port)

    try:
        packet = struct.pack('BB', command_byte & 0xFF, value_byte & 0xFF)
        _control_socket.sendall(packet)
    except Exception as e:
        print(f"Error sending command: {e}")

def send_car_instruction(car_index: int, steering: int, throttle: int, port: int):
    """
    Send driving instructions to Unity car instructions server.
    """
    global _car_socket
    if _car_socket is None:
        init_car_socket(port)

    try:
        packet = struct.pack('<I2B', car_index, steering, throttle)
        _car_socket.sendall(packet)
    except Exception as e:
        print(f"Error sending car instruction: {e}")