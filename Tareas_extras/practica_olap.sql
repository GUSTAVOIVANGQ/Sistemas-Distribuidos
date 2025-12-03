-- (c) Carlos Pineda Guerrero. 2024

create table sales_data (
  sales decimal(10,2),
  order_date date,
  product varchar(256),
  customer varchar(256),
  country varchar(256),
  region varchar(256),
  employee varchar(256),
  category varchar(256),
  weekday varchar(20),
  month varchar(20),
  quarter varchar(2),
  semester varchar(20)
);

-- Tablas de dimensiones:

CREATE TABLE region (
  id_region int NOT NULL AUTO_INCREMENT,
  region varchar(30) DEFAULT NULL,
  PRIMARY KEY (id_region)
);

CREATE TABLE country (
  id_country int NOT NULL AUTO_INCREMENT,
  country varchar(256) DEFAULT NULL,
  id_region int DEFAULT NULL,
  PRIMARY KEY (id_country),
  KEY id_region (id_region),
  FOREIGN KEY (id_region) REFERENCES region (id_region)
);

CREATE TABLE customer (
  id_customer int NOT NULL AUTO_INCREMENT,
  customer varchar(256) DEFAULT NULL,
  PRIMARY KEY (id_customer)
);

CREATE TABLE employee (
  id_employee int NOT NULL AUTO_INCREMENT,
  employee varchar(256) DEFAULT NULL,
  PRIMARY KEY (id_employee)
);

CREATE TABLE semester (
  id_semester int NOT NULL AUTO_INCREMENT,
  semester varchar(20) DEFAULT NULL,
  PRIMARY KEY (id_semester)
);

CREATE TABLE quarter (
  id_quarter int NOT NULL AUTO_INCREMENT,
  quarter varchar(2) DEFAULT NULL,
  id_semester int DEFAULT NULL,
  PRIMARY KEY (id_quarter),
  KEY id_semester (id_semester),
  FOREIGN KEY (id_semester) REFERENCES semester (id_semester)
);

CREATE TABLE month (
  id_month int NOT NULL AUTO_INCREMENT,
  month varchar(20) DEFAULT NULL,
  id_quarter int DEFAULT NULL,
  PRIMARY KEY (id_month),
  KEY id_quarter (id_quarter),
  FOREIGN KEY (id_quarter) REFERENCES quarter (id_quarter)
);

CREATE TABLE weekday (
  id_weekday int NOT NULL AUTO_INCREMENT,
  weekday varchar(20) DEFAULT NULL,
  PRIMARY KEY (id_weekday)
);

CREATE TABLE order_date (
  id_order_date int NOT NULL AUTO_INCREMENT,
  order_date date DEFAULT NULL,
  id_weekday int DEFAULT NULL,
  id_month int DEFAULT NULL,
  PRIMARY KEY (id_order_date),
  KEY id_weekday (id_weekday),
  KEY id_month (id_month),
  FOREIGN KEY (id_weekday) REFERENCES weekday (id_weekday),
  FOREIGN KEY (id_month) REFERENCES month (id_month)
);

CREATE TABLE category (
  id_category int NOT NULL AUTO_INCREMENT,
  category varchar(256) DEFAULT NULL,
  PRIMARY KEY (id_category)
);

CREATE TABLE product (
  id_product int NOT NULL AUTO_INCREMENT,
  product varchar(256) DEFAULT NULL,
  id_category int DEFAULT NULL,
  PRIMARY KEY (id_product),
  KEY id_category (id_category),
  FOREIGN KEY (id_category) REFERENCES category (id_category)
);

-- Fact table:

CREATE TABLE fact_table (
  sales decimal(10,2) DEFAULT NULL,
  id_order_date int DEFAULT NULL,
  id_product int DEFAULT NULL,
  id_customer int DEFAULT NULL,
  id_country int DEFAULT NULL,
  id_employee int DEFAULT NULL,
  KEY id_order_date (id_order_date),
  KEY id_product (id_product),
  KEY id_customer (id_customer),
  KEY id_country (id_country),
  KEY id_employee (id_employee),
  FOREIGN KEY (id_order_date) REFERENCES order_date (id_order_date),
  FOREIGN KEY (id_product) REFERENCES product (id_product),
  FOREIGN KEY (id_customer) REFERENCES customer (id_customer),
  FOREIGN KEY (id_country) REFERENCES country (id_country),
  FOREIGN KEY (id_employee) REFERENCES employee (id_employee)
);

-- Tablas agregadas:

create table country_category_product (
  id_country integer,
  id_category integer,
  id_product integer,
  sales decimal(10,2),
  FOREIGN KEY (id_country) REFERENCES country (id_country),
  FOREIGN KEY (id_category) REFERENCES category (id_category),
  FOREIGN KEY (id_product) REFERENCES product (id_product)
);

create table country_quarter_category (
  id_country integer,
  id_quarter integer,
  id_category integer,
  sales decimal(10,2),
  FOREIGN KEY (id_country) REFERENCES country (id_country),
  FOREIGN KEY (id_quarter) REFERENCES quarter (id_quarter),
  FOREIGN KEY (id_category) REFERENCES category (id_category)
);

create table country_weekday_product (
  id_country integer,
  id_weekday integer,
  id_product integer,
  sales decimal(10,2),
  FOREIGN KEY (id_country) REFERENCES country (id_country),
  FOREIGN KEY (id_weekday) REFERENCES weekday (id_weekday),
  FOREIGN KEY (id_product) REFERENCES product (id_product)
);
