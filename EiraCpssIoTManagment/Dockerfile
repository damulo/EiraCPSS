#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
#EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["WebApp/WebApp.csproj", "WebApp/"]
COPY ["BulkOperations/BulkOperations.csproj", "BulkOperations/"]
RUN dotnet restore "WebApp/WebApp.csproj"
COPY . .
#WORKDIR "/src/WebApp"
WORKDIR "/src/WebApp"
RUN dotnet build "WebApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebApp.dll"]


#FROM microsoft/dotnet:latest
#COPY . /app
#WORKDIR /app
 #
#RUN ["dotnet", "restore"]
#RUN ["dotnet", "build"]
 #
#EXPOSE 5000/tcp
#ENV ASPNETCORE_URLS http://*:5000
 #
#ENTRYPOINT ["dotnet", "run"]




#FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
#WORKDIR /app
#COPY . ./
#RUN dotnet publish "WebApp/WebApp.csproj" -c Release -o Publish
#
#FROM mcr.microsoft.com/dotnet/aspnet:6.0
#WORKDIR "/app"
#COPY --from=build /app/WebApp/Publish .
#
#ENTRYPOINT ["dotnet", "WebApp.dll"]