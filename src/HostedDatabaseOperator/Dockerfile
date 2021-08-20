# Build the operator
FROM mcr.microsoft.com/dotnet/sdk:latest as build
WORKDIR /operator

COPY ./ ./
RUN dotnet publish -c Release -o out ../HostedDatabaseOperator.csproj

# The runner for the application
FROM mcr.microsoft.com/dotnet/aspnet:latest as final
WORKDIR /operator

COPY --from=build /operator/out/ ./

ENTRYPOINT [ "dotnet", "HostedDatabaseOperator.dll" ]
