FROM mcr.microsoft.com/dotnet/sdk:11.0.100 AS build
WORKDIR /src
COPY ["ApplicationGuardrailsHosted.csproj", "packages.lock.json", "./"]
RUN dotnet restore "ApplicationGuardrailsHosted.csproj" --locked-mode
COPY ["Program.cs", "BlogTools.cs", "./"]
RUN dotnet publish "ApplicationGuardrailsHosted.csproj" -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8088
EXPOSE 8088
ENTRYPOINT ["dotnet", "ApplicationGuardrailsHosted.dll"]
