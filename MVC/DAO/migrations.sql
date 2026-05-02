-- ============================================================
-- Migración HU-003 / HU-004 / HU-005
-- Base de datos: transportes_tobon
-- Idempotente: se puede ejecutar varias veces sin error.
--
-- Resumen del estado actual (según Tobon_backup.sql):
--   * users.fecha_creacion YA existe -> no se agrega.
--   * clientes ya existe con: id, nombre_razon_social, nit_cc (UNIQUE),
--     email (UNIQUE), direccion, notas.
--   * telefonos ya existe (1:N a clientes y conductores).
--
-- Lo único que falta para HU-005 es poder desactivar clientes y
-- mostrar fecha de creación, así que sólo añadimos esas dos columnas.
-- ============================================================

-- HU-005: permitir desactivar un cliente
ALTER TABLE public.clientes
    ADD COLUMN IF NOT EXISTS estado BOOLEAN NOT NULL DEFAULT TRUE;

-- HU-005 (apoyo): fecha de creación del cliente (registros previos quedan con NOW())
ALTER TABLE public.clientes
    ADD COLUMN IF NOT EXISTS fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW();

-- Índices auxiliares para búsquedas por nombre/NIT (idempotentes)
CREATE INDEX IF NOT EXISTS idx_clientes_nombre ON public.clientes (nombre_razon_social);
CREATE INDEX IF NOT EXISTS idx_clientes_nit_cc ON public.clientes (nit_cc);
