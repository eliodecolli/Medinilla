#!/usr/bin/env python3
import os
import sys
import subprocess
from pathlib import Path

def compile_protos(type_format):
    """
    Compile all .proto files in the current directory and subdirectories.
    
    Args:
        type_format (str): The output format for protoc (e.g., 'python_out', 'csharp_out')
    """
    print(f"Compiling with protoc as {type_format}")
    current_dir = os.getcwd()
    
    # Check protoc path
    protoc_path = "./tools/protoc/protoc.exe"
    abs_protoc_path = os.path.abspath(protoc_path)
    print(f"Looking for protoc at: {abs_protoc_path}")
    
    if not os.path.exists(abs_protoc_path):
        print("Protoc not found! Listing directory contents:")
        # List contents of current directory and tools directory if it exists
        print("Current directory:", os.listdir(current_dir))
        tools_dir = os.path.join(current_dir, "tools")
        if os.path.exists(tools_dir):
            print("Tools directory:", os.listdir(tools_dir))
        sys.exit(1)
    
    # Make protoc executable
    os.chmod(abs_protoc_path, 0o755)
    
    # Create compiled directory if it doesn't exist
    compiled_dir = os.path.join(current_dir, "compiled")
    os.makedirs(compiled_dir, exist_ok=True)
    
    # Find all .proto files recursively
    for root, _, files in os.walk(current_dir):
        for file in files:
            if file.endswith('.proto'):
                proto_file = os.path.join(root, file)
                proto_path = os.path.dirname(os.path.abspath(proto_file))
                
                print(f"Compiling {file}")
                
                # Build the protoc command
                cmd = [
                    abs_protoc_path,
                    f"--proto_path={proto_path}",
                    f"--{type_format}={compiled_dir}",
                    proto_file
                ]
                
                # Run protoc
                try:
                    process = subprocess.run(cmd, check=True, capture_output=True, text=True)
                    if process.stderr:
                        print(f"Warnings for {file}:")
                        print(process.stderr)
                except subprocess.CalledProcessError as e:
                    print(f"Error compiling {file}:")
                    print(e.stderr)
                    sys.exit(1)

def main():
    if len(sys.argv) > 1:
        type_format = sys.argv[1]
        compile_protos(type_format)
        print("All done.")
    else:
        print("Invalid arguments specified:", ";".join(sys.argv))

if __name__ == "__main__":
    main()