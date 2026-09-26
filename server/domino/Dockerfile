FROM eclipse-temurin:21-jre-jammy@sha256:e9aaf73145bbd1f9f6ec7f6867dd75a44f34b1a6c32a813504bf4129be2d09d7

RUN groupadd --gid 10001 domino \
    && useradd --uid 10001 --gid domino --no-create-home --shell /usr/sbin/nologin domino
WORKDIR /app
COPY --chown=10001:10001 build/libs/domino-0.0.1-SNAPSHOT.jar /app/domino.jar
USER 10001:10001
EXPOSE 8080
ENTRYPOINT ["java", "-jar", "/app/domino.jar"]
