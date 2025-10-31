# Tarea 3 – Implementación de una VPN VNet-to-VNet en Azure  
Boleta: 2022630278  
Materia: (Completar)  
Alumno(a): (Tu nombre completo)  
Fecha: (Completar)  

---

## Índice
1. Objetivo  
2. Alcance y criterios de aceptación  
3. Fundamentación teórica  
   3.1 Virtual Network (VNet)  
   3.2 Gateway de Red Virtual  
   3.3 VPN VNet-to-VNet (Route-Based)  
   3.4 Subred `GatewaySubnet`  
   3.5 Consideraciones de direccionamiento IP  
4. Diseño de la solución  
   4.1 Nomenclatura oficial usada  
   4.2 Regiones seleccionadas  
   4.3 Plan de direccionamiento  
   4.4 Diagrama lógico  
5. Procedimiento detallado en el Portal de Azure  
   5.1 Creación de Resource Groups  
   5.2 Creación de la primera Red Virtual  
   5.3 Creación de la segunda Red Virtual  
   5.4 Creación de las subredes `GatewaySubnet`  
   5.5 Creación de IPs públicas para los Gateways  
   5.6 Creación de los Virtual Network Gateways  
   5.7 Creación de las Conexiones VNet-to-VNet  
   5.8 Creación de las Máquinas Virtuales Ubuntu  
   5.9 Configuración de reglas ICMP (NSG)  
   5.10 Verificación de conectividad (ping)  
   5.11 Visualización de topología de red  
6. Pruebas y resultados  
   6.1 Estado de gateways y conexiones  
   6.2 Pings bidireccionales  
   6.3 Rutas y latencia  
7. Problemas encontrados y soluciones aplicadas  
8. Conclusiones  
9. Eliminación (limpieza) de recursos  
10. Referencias  

---

## 1. Objetivo
Implementar una conexión segura VPN tipo VNet-to-VNet entre dos redes virtuales ubicadas en diferentes regiones de Azure, verificando la conectividad privada mediante ICMP entre dos máquinas virtuales Ubuntu, cumpliendo estrictamente la nomenclatura exigida.

## 2. Alcance y criterios de aceptación
- Dos VNets en regiones distintas sin traslape de direcciones IP.  
- Dos Virtual Network Gateways (tipo VPN, route-based).  
- Dos conexiones (una por cada lado) con estado Connected.  
- Dos VMs (Ubuntu 20.04, 1 vCPU, ~1GB RAM, 30 GB disco) en subred `default` de cada VNet.  
- Ping exitoso en ambas direcciones usando IPs privadas.  
- Capturas de todos los pasos y topologías.  
- Eliminación completa de recursos al finalizar.

## 3. Fundamentación teórica

### 3.1 Virtual Network (VNet)
Una VNet es el bloque fundamental de red lógica en Azure que permite el aislamiento y segmentación de recursos con direccionamiento privado.

### 3.2 Gateway de Red Virtual
Recurso administrado que posibilita túneles criptográficos (IPsec/IKE) y enrutamiento entre VNets, on-premises u otras nubes.

### 3.3 VPN VNet-to-VNet (Route-Based)
Establece túneles IPsec entre gateways en distintas VNets. El tipo route-based usa tablas de enrutamiento dinámicas (basadas en rutas) y es necesario para configuraciones flexibles.

### 3.4 Subred `GatewaySubnet`
Subred reservada y obligatoria para hospedar el gateway. Debe llamarse exactamente `GatewaySubnet` y contar con suficiente espacio (/27 recomendado).

### 3.5 Consideraciones de direccionamiento IP
Es indispensable evitar traslapes para que las rutas sean válidas y el tráfico fluya mediante el túnel. Espacios disjuntos simplifican la propagación.

## 4. Diseño de la solución

### 4.1 Nomenclatura oficial usada
- Primera red virtual: `T3-2022630278-vnet-1`  
- Primer gateway: `T3-2022630278-gateway-1`  
- IP pública gateway 1: `T3-2022630278-ip-1`  
- Conexión 1: `T3-2022630278-conexion-1`  
- VM 1: `T3-2022630278-1`  
- Segunda red virtual: `T3-2022630278-vnet-2`  
- Segundo gateway: `T3-2022630278-gateway-2`  
- IP pública gateway 2: `T3-2022630278-ip-2`  
- Conexión 2: `T3-2022630278-conexion-2`  
(Opcional RGs: `T3-2022630278-rg-1`, `T3-2022630278-rg-2`)

### 4.2 Regiones seleccionadas
- Ejemplo: East US (VNet 1)  
- West US (VNet 2)  
(Justificar elección por latencia o disponibilidad)

### 4.3 Plan de direccionamiento
| VNet | Space | Subred default | Subred GatewaySubnet |
|------|-------|----------------|----------------------|
| T3-2022630278-vnet-1 | 10.30.0.0/16 | 10.30.1.0/24 | 10.30.254.0/27 |
| T3-2022630278-vnet-2 | 10.40.0.0/16 | 10.40.1.0/24 | 10.40.254.0/27 |

### 4.4 Diagrama lógico
```mermaid
graph LR
  subgraph Region A (East US)
    VNet1[T3-2022630278-vnet-1]
    VM1[T3-2022630278-1\n10.30.1.x]
    GW1[T3-2022630278-gateway-1]
    VNet1 --> VM1
    VNet1 --> GW1
  end
  subgraph Region B (West US)
    VNet2[T3-2022630278-vnet-2]
    VM2[T3-2022630278-2\n10.40.1.x]
    GW2[T3-2022630278-gateway-2]
    VNet2 --> VM2
    VNet2 --> GW2
  end
  GW1 <-- IPsec Tunnel --> GW2
```

## 5. Procedimiento detallado (Portal Azure)

> Nota: A continuación se describen pasos. En el PDF incluir cada captura con un pie de figura: “Figura X. [Descripción].”

### 5.1 Creación de Resource Groups
1. Azure Portal > Resource groups > Create.  
2. Nombre: `T3-2022630278-rg-1`, Región: (ej. East US).  
3. Repetir para `T3-2022630278-rg-2` (otra región).  
(Captura)

### 5.2 Crear primera Red Virtual
1. Create resource > Virtual Network.  
2. Nombre: `T3-2022630278-vnet-1`, RG: `T3-2022630278-rg-1`, Región: East US.  
3. Address space: 10.30.0.0/16.  
4. Subnet `default`: 10.30.1.0/24.  
5. Crear.  
(Captura resumen)

### 5.3 Crear segunda Red Virtual
Igual que anterior con datos de `T3-2022630278-vnet-2`, espacio 10.40.0.0/16, subred default 10.40.1.0/24, RG2.  
(Captura)

### 5.4 Crear subredes GatewaySubnet
En cada VNet > Subnets > + Gateway subnet  
- Address range: 10.30.254.0/27 y 10.40.254.0/27.  
(Capturas separadas)

### 5.5 Crear IPs públicas para gateways
Create resource > Public IP  
- Nombre: `T3-2022630278-ip-1`, SKU Standard, Static.  
- Repetir: `T3-2022630278-ip-2`.  
(Capturas)

### 5.6 Crear Virtual Network Gateway 1
1. Create resource > Virtual network gateway.  
2. Nombre: `T3-2022630278-gateway-1`.  
3. Gateway type: VPN. VPN type: Route-based. SKU: VpnGw1 (o Basic si limitación).  
4. VNet: seleccionar `T3-2022630278-vnet-1`.  
5. Public IP: `T3-2022630278-ip-1`.  
6. Crear y esperar (30–45 min).  
(Captura en estado Succeeded)

### 5.7 Crear Virtual Network Gateway 2
Igual con `T3-2022630278-gateway-2` y `T3-2022630278-ip-2`.  
(Captura)

### 5.8 Crear Conexiones VNet-to-VNet
Ir a `T3-2022630278-gateway-1` > Connections > + Add  
- Nombre: `T3-2022630278-conexion-1`  
- Connection type: VNet-to-VNet  
- Seleccionar gateway remoto: `T3-2022630278-gateway-2`  
- Shared key (PSK): (documentar) `ClavePSK2022630278!`  
Crear (Captura)

Repetir desde gateway 2:  
- Nombre: `T3-2022630278-conexion-2`  
- Gateway remoto: `T3-2022630278-gateway-1`  
- Misma PSK.  
(Captura estado Connected de ambas)

### 5.9 Crear Máquinas Virtuales
VM 1:
1. Create resource > Virtual Machine.  
2. Nombre: `T3-2022630278-1`, RG1, Región (misma que VNet 1), Imagen: Ubuntu 20.04 LTS.  
3. Size: Standard_B1s.  
4. Disco OS: 30 GiB (Standard SSD/HDD).  
5. Red: VNet `T3-2022630278-vnet-1`, Subnet: default.  
6. Asignar o crear IP pública (para SSH).  
7. Autenticación: SSH Key (recomendado).  
(Captura antes de crear y luego panel Overview)

VM 2:
Repetir con `T3-2022630278-2` en VNet 2.  
(Captura)

### 5.10 Configurar ICMP (NSG)
En cada VM > Networking:  
- Agregar regla Inbound:  
  - Protocol: ICMP  
  - Source: Any  
  - Destination: Any  
  - Action: Allow  
  - Priority: 350  
  - Name: allow-icmp  
(Capturas de regla por cada VM)

### 5.11 Verificación de conectividad
1. Obtener IP privada VM2 (ej. 10.40.1.x).  
2. SSH a VM1: `ssh azureuser@<IP_PUBLICA_VM1>`  
3. Comando: `ping -c 4 10.40.1.x` (Captura)  
4. Repetir inverso desde VM2 hacia IP privada de VM1.  
5. (Opcional) `traceroute <IP>` para mostrar salto virtual (instalar `sudo apt update && sudo apt install traceroute -y`).  
(Capturas)

### 5.12 Visualizar topologías
En cada VM > Configuración de red > Ver topología.  
(Capturas de ambas topologías)

---

## 6. Pruebas y resultados

### 6.1 Estado de gateways y conexiones
- Ambos gateways: Succeeded  
- `T3-2022630278-conexion-1`: Connected  
- `T3-2022630278-conexion-2`: Connected  
(Captura consolidada)

### 6.2 Pings bidireccionales
| Origen | Destino | Paquetes recibidos | Latencia media (ms) |
|--------|---------|--------------------|---------------------|
| VM1 (10.30.1.x) | VM2 (10.40.1.x) | 4/4 | (Completar) |
| VM2 (10.40.1.x) | VM1 (10.30.1.x) | 4/4 | (Completar) |

### 6.3 Rutas y latencia
Agregar salidas de comandos si se usaron (`ip route`, `traceroute`).  
(Captura)

---

## 7. Problemas encontrados y soluciones aplicadas
| Problema | Causa | Solución | Evidencia |
|----------|-------|----------|-----------|
| Ej. Conexión tardó en conectar | Propagación túnel | Esperar 5–10 min | (Captura) |
| Ej. Ping falló inicialmente | Falta regla ICMP | Crear regla NSG | (Captura) |

(Completar según experiencia real)

---

## 8. Conclusiones
(Orientación: comentar importancia de no traslapar IPs, tiempos de aprovisionamiento, latencia inter-regiones, diferencia VPN vs Peering, relevancia de GatewaySubnet y costo de gateways. Redactar en párrafos.)

Ejemplo (ajustar):
La práctica permitió comprender el flujo completo para establecer conectividad privada entre regiones mediante VPN site-to-site administrada (VNet-to-VNet). Se evidenció que el tiempo de aprovisionamiento de gateways es un factor crítico de planificación. La latencia observada fue coherente con la distancia regional y la comunicación se logró sin traslape de espacios IP. Finalmente, la importancia de liberar recursos se reflejó en la optimización de créditos de la suscripción Azure for Students.

---

## 9. Eliminación (limpieza) de recursos
Orden ejecutado:  
1. Eliminación de conexiones.  
2. Eliminación de gateways (esperar finalización).  
3. Eliminación de VMs (+ discos/IP públicas asociadas).  
4. Eliminación de IPs públicas sobrantes.  
5. Eliminación de VNets.  
6. Eliminación de Resource Groups.  
(Capturas opcionales de confirmación)

---

## 10. Referencias
- Documentación Azure VPN Gateway: [Azure VPN Gateway Documentation](https://learn.microsoft.com/azure/vpn-gateway/)  
- Creación de conexiones VNet a VNet: [Connect virtual networks with a VNet-to-VNet connection](https://learn.microsoft.com/azure/vpn-gateway/vpn-gateway-howto-vnet-vnet-resource-manager-portal)  
- Direccionamiento recomendado: [Virtual network planning and design](https://learn.microsoft.com/azure/virtual-network/virtual-network-vnet-plan-design-arm)  

---

## Anexos (Opcional)
### A. Lista de verificaciones previas a la entrega
- [ ] Nombres exactos según boleta  
- [ ] Espacios de direcciones sin traslape  
- [ ] Gateways en estado Succeeded  
- [ ] Conexiones en estado Connected  
- [ ] Ping exitoso en ambas direcciones  
- [ ] Capturas completas y legibles  
- [ ] Topologías capturadas  
- [ ] Recursos eliminados (evidencia)  

### B. Placeholders de capturas (reemplazar con imágenes reales)
(Usar nombres: Figura 1, Figura 2, etc.)

---

(Insertar aquí cada captura con su pie:  
Figura X. Creación de la red virtual T3-2022630278-vnet-1.  
Figura Y. Resultado del ping desde VM1 a VM2.  
... )

---

Fin del documento.