#!/bin/bash

# Detener el script en caso de error
set -e

# Variables
TOMCAT_VERSION="8.5.99"
JERSEY_VERSION="2.24"
GSON_VERSION="2.3.1"
MYSQL_CONNECTOR_VERSION="9.4.0"

TOMCAT_DIR="apache-tomcat-${TOMCAT_VERSION}"
TOMCAT_ZIP="${TOMCAT_DIR}.zip"
TOMCAT_URL="https://archive.apache.org/dist/tomcat/tomcat-8/v${TOMCAT_VERSION}/bin/${TOMCAT_ZIP}"

JERSEY_ZIP="jaxrs-ri-${JERSEY_VERSION}.zip"
JERSEY_URL="https://repo1.maven.org/maven2/org/glassfish/jersey/bundles/jaxrs-ri/${JERSEY_VERSION}/${JERSEY_ZIP}"

GSON_JAR="gson-${GSON_VERSION}.jar"
GSON_URL="https://repo1.maven.org/maven2/com/google/code/gson/gson/${GSON_VERSION}/${GSON_JAR}"

MYSQL_ZIP="mysql-connector-j-${MYSQL_CONNECTOR_VERSION}.zip"
MYSQL_URL="https://dev.mysql.com/get/Downloads/Connector-J/${MYSQL_ZIP}"

# 1. Actualizar paquetes e instalar JDK8
echo "Instalando JDK 8..."
sudo apt update
sudo apt install -y openjdk-8-jdk-headless unzip wget

# 2. Descargar Tomcat 8
echo "Descargando Tomcat ${TOMCAT_VERSION}..."
wget -q "${TOMCAT_URL}"

# 3. Desempaquetar Tomcat
echo "Desempaquetando Tomcat..."
unzip -q "${TOMCAT_ZIP}"

# 4. Eliminar y recrear webapps/ROOT
echo "Reconfigurando directorio webapps..."
rm -rf "${TOMCAT_DIR}/webapps"
mkdir -p "${TOMCAT_DIR}/webapps/ROOT"

# 5. Descargar Jersey
echo "Descargando Jersey ${JERSEY_VERSION}..."
wget -q "${JERSEY_URL}"
unzip -q "${JERSEY_ZIP}" -d "jersey"

# 6. Copiar archivos .jar de Jersey a Tomcat/lib
echo "Copiando bibliotecas Jersey a Tomcat/lib..."
find jersey -name "*.jar" -exec cp {} "${TOMCAT_DIR}/lib/" \;

# 7. Eliminar javax.servlet-api-3.0.1.jar
JAR_TO_REMOVE="${TOMCAT_DIR}/lib/javax.servlet-api-3.0.1.jar"
if [ -f "$JAR_TO_REMOVE" ]; then
    echo "Eliminando ${JAR_TO_REMOVE}..."
    rm "$JAR_TO_REMOVE"
fi

# 8. Descargar Gson
echo "Descargando Gson ${GSON_VERSION}..."
wget -q "${GSON_URL}" -O "${GSON_JAR}"
cp "${GSON_JAR}" "${TOMCAT_DIR}/lib/"

# 9. Descargar MySQL Connector
echo "Descargando MySQL Connector/J ${MYSQL_CONNECTOR_VERSION}..."
wget -q "${MYSQL_URL}"
unzip -q "${MYSQL_ZIP}"

# 10. Copiar el archivo .jar del conector MySQL
MYSQL_JAR_PATH=$(find "mysql-connector-j-${MYSQL_CONNECTOR_VERSION}" -name "mysql-connector-j-*.jar" | head -n 1)
if [ -f "$MYSQL_JAR_PATH" ]; then
    echo "Copiando MySQL Connector a Tomcat/lib..."
    cp "$MYSQL_JAR_PATH" "${TOMCAT_DIR}/lib/"
else
    echo "ERROR: No se encontró el JAR del conector MySQL"
    exit 1
fi

# 11. Limpieza opcional
echo "Limpieza de archivos descargados..."
rm -f "${TOMCAT_ZIP}" "${JERSEY_ZIP}" "${GSON_JAR}" "${MYSQL_ZIP}"
rm -rf "jersey" "mysql-connector-j-${MYSQL_CONNECTOR_VERSION}"

echo "✅ Configuración completada correctamente. Tomcat está listo en: ${TOMCAT_DIR}"
