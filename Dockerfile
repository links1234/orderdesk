FROM node:22-slim AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npx ng build --configuration production

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/OrderDesk.Api/OrderDesk.Api.csproj src/OrderDesk.Api/
RUN dotnet restore src/OrderDesk.Api
COPY src/ src/
RUN dotnet publish src/OrderDesk.Api -c Release -o /app --no-restore
COPY --from=web /web/dist/web/browser /app/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderDesk.Api.dll"]
