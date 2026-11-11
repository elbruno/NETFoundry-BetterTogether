FROM mcr.microsoft.com/dotnet/sdk:11.0.100 AS build
WORKDIR /src
COPY ["ApplicationGuardrails.csproj", "packages.lock.json", "./"]
RUN dotnet restore "ApplicationGuardrails.csproj" --locked-mode
COPY . .
RUN dotnet publish "ApplicationGuardrails.csproj" --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8088
EXPOSE 8088
ENTRYPOINT ["dotnet", "ApplicationGuardrails.dll"]
