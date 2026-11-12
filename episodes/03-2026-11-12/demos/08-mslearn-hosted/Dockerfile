FROM mcr.microsoft.com/dotnet/sdk:11.0.100 AS build
WORKDIR /src
COPY ["LearnHosted.csproj", "packages.lock.json", "./"]
RUN dotnet restore "LearnHosted.csproj" --locked-mode
COPY Program.cs ./
RUN dotnet publish "LearnHosted.csproj" -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8088
EXPOSE 8088
ENTRYPOINT ["dotnet", "LearnHosted.dll"]
