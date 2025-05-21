# Use a .NET runtime base image that is compatible with your application
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "AsteriskAMIStream.dll"]
