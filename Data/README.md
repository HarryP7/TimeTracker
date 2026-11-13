
## Генерация миграции
в консоли диспретчера пакетов:
> `Add-Migration initDb -OutputDir Data/Migrations`

## Применение миграции:
> `Update-Database`

## Миграции
### 1:
> `dotnet ef migrations add AddPauseStartedAtToGeneralInfoTimeDay`

### 2:
> `dotnet ef migrations add AddWorkEndTimeToGeneralInfoTimeDay`

### 3:
> `Add-Migration AddTimeBalanceSecondsToGeneralInfoTimeDay`