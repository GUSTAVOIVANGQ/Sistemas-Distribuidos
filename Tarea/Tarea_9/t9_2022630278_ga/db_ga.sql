```sql
CREATE TABLE IF NOT EXISTS stock (
  id_articulo INT AUTO_INCREMENT PRIMARY KEY,
  nombre VARCHAR(200) NOT NULL,
  descripcion TEXT NOT NULL,
  precio DECIMAL(10,2) NOT NULL
);
CREATE TABLE IF NOT EXISTS fotos_articulos (
  id_foto INT AUTO_INCREMENT PRIMARY KEY,
  foto LONGBLOB NOT NULL,
  id_articulo INT NOT NULL,
  FOREIGN KEY (id_articulo) REFERENCES stock(id_articulo) ON DELETE CASCADE
);
```