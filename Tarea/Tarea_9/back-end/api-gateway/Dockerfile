FROM eclipse-temurin:17-jdk
WORKDIR /app
COPY SimpleAPIGateway.java .
RUN javac SimpleAPIGateway.java
# No pongas secretos en ENV; se inyectan en Kubernetes
ENV keystore=/keystore/keystore_servidor.jks
ENV password=1234567
CMD ["java","SimpleAPIGateway"]