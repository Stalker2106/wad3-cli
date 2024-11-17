FROM debian:bookworm-slim

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

RUN apt update && apt install -y wget && \
    wget https://github.com/Stalker2106x/wad3-cli/releases/download/v1.0.0/wad3-cli-linux_x86-64 && \
    mv wad3-cli-linux_x86-64 /usr/local/bin/wad3-cli && \
    chmod +x /usr/local/bin/wad3-cli