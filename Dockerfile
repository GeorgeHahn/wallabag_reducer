FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# copy and publish app and libraries
COPY . ./
RUN dotnet ef database update
RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:2.2-runtime AS runtime
WORKDIR /app
COPY --from=build /app/out ./
COPY --from=build /app/wallabag_reducer.sqlite3 ./
ENTRYPOINT ["dotnet", "WallabagReducer.Net.dll"]