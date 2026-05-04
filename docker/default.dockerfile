# NOTE: needs to be run in context of root repository folder (not project folder)

FROM mcr.microsoft.com/dotnet/aspnet:8.0

ARG EXPOSED_PORT=80
ARG PROJECT_NAME=UnknownProject
ARG PROJECT_PATH=./.output/${PROJECT_NAME}

WORKDIR /app
COPY ${PROJECT_PATH} .
RUN echo "dotnet ${PROJECT_NAME}.dll \"\$@\"" > /app/entrypoint.sh && chmod 744 /app/entrypoint.sh
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:${EXPOSED_PORT}
EXPOSE ${EXPOSED_PORT}
ENTRYPOINT ["bash", "./entrypoint.sh"]
