FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build
COPY . /tmp/build
WORKDIR /tmp/build
RUN dotnet restore
RUN dotnet publish -o output

FROM mcr.microsoft.com/dotnet/core/runtime:2.2 AS runtime
WORKDIR /app
COPY --from=build /tmp/build/output /app
ENTRYPOINT ["dotnet", "test-app.dll"]