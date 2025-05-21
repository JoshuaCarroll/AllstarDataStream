FROM mcr.microsoft.com/dotnet/runtime:9.0-preview-arm32v7
WORKDIR /app
COPY publish/ ./
ENTRYPOINT ["./AsteriskAMIStream"]
