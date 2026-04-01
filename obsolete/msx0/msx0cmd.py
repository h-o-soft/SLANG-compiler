#!/usr/bin/env python3
# msx0cmd.py : MSX0 send 1 line command

import socket
import argparse
import time

def send_command(host, port, message, check_str, error_str):
    data = ""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.connect((host, port))
        s.sendall((message+'\r\n').encode())
        
        if check_str or error_str:
            chunks = []
            while True:
                part = s.recv(1024)
                if not part:
                    break
                chunks.append(part.decode())
                data = ''.join(chunks)
                if check_str and check_str in data:
                    return 0  # 正常終了
                elif error_str and error_str in data:
                    return 1 # 異常終了

    if check_str:
        return 1  # 異常終了
    else:
        return 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Send a message to a specific IP and port.')
    
    parser.add_argument('message', type=str, help='The message to send.')
    parser.add_argument('--ip', type=str, required=True, help='The target IP address.')
    parser.add_argument('--port', type=int, default=2223, help='The target port. Default is 2223.')
    parser.add_argument("--check-str", help="String to check for in the response. If this string is not present, the script will exit with an error status.")
    parser.add_argument("--error-str", help="Error string to check for in the response. If found, exits with error.")
    parser.add_argument('--sleep', type=int, default=None, help='Sleep for a specified number of seconds after sending the message.')
    
    args = parser.parse_args()
    
    exit_status = send_command(args.ip, args.port, args.message, args.check_str, args.error_str)

    if args.sleep:
        time.sleep(args.sleep)

    exit(exit_status)
