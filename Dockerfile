FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BackendCodeChallengeApi/BackendCodeChallengeApi.csproj BackendCodeChallengeApi/
RUN dotnet restore BackendCodeChallengeApi/BackendCodeChallengeApi.csproj

COPY BackendCodeChallengeApi/ BackendCodeChallengeApi/
RUN dotnet publish BackendCodeChallengeApi/BackendCodeChallengeApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BackendCodeChallengeApi.dll"]