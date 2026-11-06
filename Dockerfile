FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Models/ Models/
COPY Data_Access_Layer/ Data_Access_Layer/
COPY Business_Logic_Layer/ Business_Logic_Layer/
COPY UniversityTimetable.API/ UniversityTimetable.API/
COPY ["University Timetable and Classroom Management System/PresentationLayer/ImportUiText.cs", "University Timetable and Classroom Management System/PresentationLayer/"]
RUN printf '{"ConnectionStrings":{"UniversityTimetable":""}}\n' > "University Timetable and Classroom Management System/database-settings.json"
RUN dotnet publish UniversityTimetable.API/UniversityTimetable.API.csproj -c Release -o /app/publish --no-self-contained \
    && rm -f /app/publish/database-settings.json

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 fonts-noto-core fonts-noto-extra && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish/ ./
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "UniversityTimetable.API.dll"]
