## Descripción

<!-- Explica el propósito de este PR en 1-3 frases. ¿Qué problema resuelve o qué funcionalidad añade? -->

## Tipo de cambio

- [ ] Bugfix (cambio no disruptivo que resuelve un issue)
- [ ] Feature (cambio no disruptivo que añade funcionalidad)
- [ ] Breaking change (cambio que rompe compatibilidad existente)
- [ ] Refactor / cleanup (sin cambio de comportamiento)
- [ ] Infra / CI / configuración

## Checklist

### Código
- [ ] El código sigue las convenciones del `.editorconfig` y el estilo del proyecto
- [ ] No hay `TODO` / `FIXME` sin issue asociado
- [ ] No hay secretos, contraseñas ni tokens hardcodeados
- [ ] Se han eliminado logs y comentarios de depuración

### Tests
- [ ] Se han añadido o actualizado tests para cubrir los cambios
- [ ] Todos los tests existentes siguen pasando (`dotnet test` / `npm run test:ci`)
- [ ] La cobertura de código no baja del 70%

### Backend (.NET)
- [ ] Las migraciones de base de datos están incluidas si corresponde
- [ ] Los endpoints nuevos están documentados en Swagger
- [ ] Los comandos/queries MediatR tienen su validator FluentValidation

### PWA
- [ ] No hay errores de TypeScript (`npm run type-check`)
- [ ] No hay warnings de ESLint (`npm run lint`)
- [ ] Los componentes nuevos son responsivos

### Documentación
- [ ] El README está actualizado si el setup cambia
- [ ] Los cambios breaking están documentados en CHANGELOG o descripción del PR

## Screenshots (si aplica)

<!-- Añade capturas de pantalla para cambios en la UI -->

## Issues relacionados

<!-- Closes #123 -->
