# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Copy the project file first so the restore layer is cached until the packages change
COPY RaceDayAPI.csproj ./
RUN dotnet restore RaceDayAPI.csproj
COPY . ./
RUN dotnet publish RaceDayAPI.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime stage (small image, no SDK, non-root user) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
USER app
ENTRYPOINT ["dotnet", "RaceDayAPI.dll"]
