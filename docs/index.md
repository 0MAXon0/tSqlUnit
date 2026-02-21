# TSqlUnit Documentation

Это стартовая страница. Основная API-документация генерируется из XML-комментариев C# кода.

## Где API

- Открой раздел **API (из XML-комментариев)** в левом меню.
- Или перейди напрямую: [API Namespace](api/TSqlUnit.yml).

## Как пересобрать API-доки

```powershell
docfx metadata docs/docfx.json
docfx build docs/docfx.json
docfx serve docs/_site
```

Открыть сайт: `http://localhost:8080`
