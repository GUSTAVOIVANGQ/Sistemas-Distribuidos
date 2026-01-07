CREATE TABLE IF NOT EXISTS usuarios (
  id_usuario INT AUTO_INCREMENT PRIMARY KEY,
  email VARCHAR(255) UNIQUE NOT NULL,
  password VARCHAR(128) NOT NULL,
  nombre VARCHAR(100) NOT NULL,
  apellido_paterno VARCHAR(100) NOT NULL,
  apellido_materno VARCHAR(100),
  fecha_nacimiento DATETIME NOT NULL,
  telefono BIGINT,
  genero CHAR(1),
  token VARCHAR(64)
);
CREATE TABLE IF NOT EXISTS fotos_usuarios (
  id_foto INT AUTO_INCREMENT PRIMARY KEY,
  foto LONGBLOB NOT NULL,
  id_usuario INT NOT NULL,
  FOREIGN KEY (id_usuario) REFERENCES usuarios(id_usuario) ON DELETE CASCADE
);