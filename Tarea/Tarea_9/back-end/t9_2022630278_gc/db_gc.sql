CREATE TABLE IF NOT EXISTS stock (
  id_articulo INT PRIMARY KEY,
  cantidad INT NOT NULL,
  UNIQUE KEY uq_stock_id_articulo (id_articulo)
);
CREATE TABLE IF NOT EXISTS carrito_compra (
  id_usuario INT NOT NULL,
  id_articulo INT NOT NULL,
  cantidad INT NOT NULL,
  UNIQUE KEY uq_carrito (id_usuario, id_articulo)
);