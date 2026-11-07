# مرحلة البناء
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/AzureDemo.Api/*.csproj src/AzureDemo.Api/
RUN dotnet restore src/AzureDemo.Api/AzureDemo.Api.csproj
COPY . .
RUN dotnet publish src/AzureDemo.Api -c Release -o /app

# مرحلة التشغيل
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AzureDemo.Api.dll"]