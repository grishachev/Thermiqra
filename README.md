# Thermiqra

Thermiqra — настольное приложение для мониторинга оборудования ПК под Windows 10/11.

## Возможности

- мониторинг CPU, GPU, RAM, накопителей и логических дисков;
- температурные предупреждения и критические уведомления;
- работа в системном трее;
- автозапуск через Планировщик заданий Windows с повышенными правами;
- сохранение настроек, размера и положения окна;
- несколько цветовых тем интерфейса;
- установщик Inno Setup с установкой PawnIO.

## Технологии

- C# / WPF
- .NET 10
- LibreHardwareMonitorLib 0.9.6
- PawnIO
- Inno Setup

## Сборка

Основной проект: `PCHardwareMonitor/PCHardwareMonitor.csproj`.

Для Release используется `win-x64` и self-contained публикация.

```powershell
dotnet publish .\PCHardwareMonitor\PCHardwareMonitor.csproj -c Release -r win-x64 --self-contained true -o .\PCHardwareMonitor\bin\Release\net10.0-windows\win-x64\publish
```

## Установщик

Скрипт Inno Setup: `Installer/Thermiqra.iss`.

Файл `PawnIO_setup.exe` намеренно не хранится в репозитории. Для локальной сборки установщика он должен находиться по пути `Installer/Dependencies/PawnIO_setup.exe`.

## Примечание

Внутреннее имя проекта и namespace пока остаются `PCHardwareMonitor`.
