#!/bin/bash
set -euo pipefail

# Versiones probadas con Java 8
TOMCAT_VERSION="8.5.99"
JERSEY_VERSION="2.24"
GSON_VERSION="2.3.1"
# IMPORTANTE: 8.4.0 es compatible con Java 8; 9.x requiere Java 17
MYSQL_CONNECTOR_VERSION="8.4.0"

TOMCAT_DIR="apache-tomcat-${TOMCAT_VERSION}"
TOMCAT_ZIP="${TOMCAT_DIR}.zip"
TOMCAT_URL="https://archive.apache.org/dist/tomcat/tomcat-8/v${TOMCAT_VERSION}/bin/${TOMCAT_ZIP}"

JERSEY_ZIP="jaxrs-ri-${JERSEY_VERSION}.zip"
JERSEY_URL="https://repo1.maven.org/maven2/org/glassfish/jersey/bundles/jaxrs-ri/${JERSEY_VERSION}/${JERSEY_ZIP}"

GSON_JAR="gson-${GSON_VERSION}.jar"
GSON_URL="https://repo1.maven.org/maven2/com/google/code/gson/gson/${GSON_VERSION}/${GSON_JAR}"

MYSQL_ZIP="mysql-connector-j-${MYSQL_CONNECTOR_VERSION}.zip"
MYSQL_URL="https://dev.mysql.com/get/Downloads/Connector-J/${MYSQL_ZIP}"

echo "==> Instalando utilidades"
sudo apt update -y
sudo apt install -y unzip wget ca-certificates gnupg curl

echo "==> Instalando Java 8 (OpenJDK), con fallback a Azul Zulu si falla"
if sudo apt install -y openjdk-8-jdk-headless; then
  echo "OpenJDK 8 instalado."
else
  echo "OpenJDK 8 no disponible. Instalando Azul Zulu 8..."
  curl -fsSL https://repos.azul.com/azul-repo.key | sudo gpg --dearmor -o /usr/share/keyrings/azul.gpg
  echo "deb [signed-by=/usr/share/keyrings/azul.gpg] https://repos.azul.com/azure-only/zulu/apt stable main" | sudo tee /etc/apt/sources.list.d/zulu.list
  sudo apt update -y
  sudo apt install -y zulu8-jdk
fi

echo "JAVA en: $(which java)"
java -version

echo "==> Descargando Tomcat ${TOMCAT_VERSION}"
wget -q "${TOMCAT_URL}"
unzip -q "${TOMCAT_ZIP}"

echo "==> Reconfigurando webapps (limpieza de apps vulnerables)"
rm -rf "${TOMCAT_DIR}/webapps"
mkdir -p "${TOMCAT_DIR}/webapps/ROOT"

echo "==> Descargando Jersey ${JERSEY_VERSION}"
wget -q "${JERSEY_URL}"
unzip -q "${JERSEY_ZIP}" -d "jersey"

echo "==> Copiando jars de Jersey a Tomcat/lib"
find jersey -type f -name "*.jar" -exec cp {} "${TOMCAT_DIR}/lib/" \;

echo "==> Eliminando javax.servlet-api-3.0.1.jar si existe (incompatibilidad Jersey)"
JAR_TO_REMOVE="${TOMCAT_DIR}/lib/javax.servlet-api-3.0.1.jar"
[ -f "$JAR_TO_REMOVE" ] && rm -f "$JAR_TO_REMOVE" || true

echo "==> Descargando Gson ${GSON_VERSION}"
wget -q "${GSON_URL}" -O "${GSON_JAR}"
cp "${GSON_JAR}" "${TOMCAT_DIR}/lib/"

echo "==> Descargando MySQL Connector/J ${MYSQL_CONNECTOR_VERSION} (compatible Java 8)"
wget -q "${MYSQL_URL}"
unzip -q "${MYSQL_ZIP}"
MYSQL_JAR_PATH=$(find "mysql-connector-j-${MYSQL_CONNECTOR_VERSION}" -type f -name "mysql-connector-j-*.jar" | head -n 1)
if [ -z "${MYSQL_JAR_PATH}" ] || [ ! -f "${MYSQL_JAR_PATH}" ]; then
  echo "ERROR: No se encontró el JAR de MySQL Connector/J ${MYSQL_CONNECTOR_VERSION}"
  exit 1
fi
cp "${MYSQL_JAR_PATH}" "${TOMCAT_DIR}/lib/"

echo "==> Configurando variables de entorno en ~/.bashrc"
grep -q "export CATALINA_HOME=" ~/.bashrc || echo "export CATALINA_HOME=\$HOME/${TOMCAT_DIR}" >> ~/.bashrc
grep -q "export JAVA_HOME=" ~/.bashrc || echo "export JAVA_HOME=/usr" >> ~/.bashrc
export CATALINA_HOME="$HOME/${TOMCAT_DIR}"
export JAVA_HOME="/usr"

echo "==> Limpieza"
rm -f "${TOMCAT_ZIP}" "${JERSEY_ZIP}" "${GSON_JAR}" "${MYSQL_ZIP}"
rm -rf "jersey" "mysql-connector-j-${MYSQL_CONNECTOR_VERSION}"

echo "✅ Listo. Tomcat en: ${TOMCAT_DIR}"
echo "Para iniciar: sh \$CATALINA_HOME/bin/catalina.sh start"