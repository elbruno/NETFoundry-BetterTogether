FROM mcr.microsoft.com/dotnet/sdk:11.0.100 AS build
WORKDIR /src
COPY ["WikipediaHosted.csproj", "packages.lock.json", "./"]
RUN dotnet restore "WikipediaHosted.csproj" --locked-mode
COPY Program.cs ./
RUN dotnet publish "WikipediaHosted.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8088
EXPOSE 8088
ENTRYPOINT ["dotnet", "WikipediaHosted.dll"]
