# Use the latest .NET SDK image as base
FROM mcr.microsoft.com/dotnet/sdk:8.0

# Install required packages
RUN apt-get update && apt-get install -y \
    wget \
    unzip \
    python3 \
    python3-pip \
    && rm -rf /var/lib/apt/lists/*

# Download and install protoc
RUN wget https://github.com/protocolbuffers/protobuf/releases/download/v29.2/protoc-29.2-linux-x86_64.zip -O /tmp/protoc.zip \
    && unzip /tmp/protoc.zip -d /tmp/protoc \
    && mv /tmp/protoc/bin/protoc /usr/local/bin/ \
    && chmod +x /usr/local/bin/protoc \
    && rm -rf /tmp/protoc /tmp/protoc.zip

# Set working directory to the mounted path
WORKDIR /app

# Copy everything from the current directory to the container
COPY . .

# Run the .NET application
CMD ["dotnet", "build"]