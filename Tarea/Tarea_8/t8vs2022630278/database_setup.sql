-- Script de creación de base de datos para el proyecto E-commerce Serverless
-- Generado por GitHub Copilot basado en el análisis del código fuente C#

CREATE DATABASE IF NOT EXISTS servicio_web;
USE servicio_web;

-- 1. Tabla de Usuarios
CREATE TABLE IF NOT EXISTS usuarios (
    id_usuario INT AUTO_INCREMENT PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    password VARCHAR(255) NOT NULL,
    nombre VARCHAR(100) NOT NULL,
    apellido_paterno VARCHAR(100) NOT NULL,
    apellido_materno VARCHAR(100),
    fecha_nacimiento DATETIME NOT NULL,
    telefono BIGINT,
    genero CHAR(20), -- Se ajustó a 20 por si guardan "Masculino" en lugar de "M" (el código usa M/F pero mejor prevenir)
    token VARCHAR(255)
) ENGINE=InnoDB;

-- 2. Tabla de Fotos de Usuarios
CREATE TABLE IF NOT EXISTS fotos_usuarios (
    id_foto INT AUTO_INCREMENT PRIMARY KEY,
    foto LONGBLOB,
    id_usuario INT NOT NULL,
    CONSTRAINT fk_foto_usuario FOREIGN KEY (id_usuario) 
        REFERENCES usuarios(id_usuario) 
        ON DELETE CASCADE 
        ON UPDATE CASCADE
) ENGINE=InnoDB;

-- 3. Tabla de Stock (Artículos)
CREATE TABLE IF NOT EXISTS stock (
    id_articulo INT AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(150) NOT NULL,
    descripcion TEXT NOT NULL,
    precio DECIMAL(10,2) NOT NULL,
    cantidad INT NOT NULL
) ENGINE=InnoDB;

-- 4. Tabla de Fotos de Artículos
CREATE TABLE IF NOT EXISTS fotos_articulos (
    id_foto_articulo INT AUTO_INCREMENT PRIMARY KEY,
    foto LONGBLOB,
    id_articulo INT NOT NULL,
    CONSTRAINT fk_foto_articulo FOREIGN KEY (id_articulo) 
        REFERENCES stock(id_articulo) 
        ON DELETE CASCADE 
        ON UPDATE CASCADE
) ENGINE=InnoDB;

-- 5. Tabla de Carrito de Compra
CREATE TABLE IF NOT EXISTS carrito_compra (
    id_carrito INT AUTO_INCREMENT PRIMARY KEY,
    id_usuario INT NOT NULL,
    id_articulo INT NOT NULL,
    cantidad INT NOT NULL,
    -- Asegura una sola entrada por artículo por usuario
    UNIQUE KEY uk_carrito_usuario_articulo (id_usuario, id_articulo),
    CONSTRAINT fk_carrito_usuario FOREIGN KEY (id_usuario) 
        REFERENCES usuarios(id_usuario) 
        ON DELETE CASCADE 
        ON UPDATE CASCADE,
    CONSTRAINT fk_carrito_articulo FOREIGN KEY (id_articulo) 
        REFERENCES stock(id_articulo) 
        ON DELETE CASCADE 
        ON UPDATE CASCADE
) ENGINE=InnoDB;

-- 6. Tabla de Ordenes (Cabecera)
CREATE TABLE IF NOT EXISTS ordenes (
    id_orden INT AUTO_INCREMENT PRIMARY KEY,
    id_usuario INT NOT NULL,
    fecha DATETIME NOT NULL,
    total DECIMAL(10,2) NOT NULL,
    CONSTRAINT fk_orden_usuario FOREIGN KEY (id_usuario) 
        REFERENCES usuarios(id_usuario) 
        ON DELETE CASCADE 
        ON UPDATE CASCADE
) ENGINE=InnoDB;

-- 7. Tabla de Detalle de Orden
CREATE TABLE IF NOT EXISTS orden_detalle (
    id_detalle INT AUTO_INCREMENT PRIMARY KEY,
    id_orden INT NOT NULL,
    id_articulo INT NOT NULL,
    cantidad INT NOT NULL,
    precio_unitario DECIMAL(10,2) NOT NULL,
    subtotal DECIMAL(10,2) NOT NULL,
    CONSTRAINT fk_detalle_orden FOREIGN KEY (id_orden) 
        REFERENCES ordenes(id_orden) 
        ON DELETE CASCADE 
        ON UPDATE CASCADE,
    CONSTRAINT fk_detalle_articulo FOREIGN KEY (id_articulo) 
        REFERENCES stock(id_articulo) 
        ON DELETE CASCADE 
        ON UPDATE CASCADE
) ENGINE=InnoDB;
