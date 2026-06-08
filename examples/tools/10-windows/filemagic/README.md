# filemagic

Classifies a file from leading bytes (`argv[1]`): PDF, PNG, ZIP magics, otherwise printable scan → `text` or `binary`.

## Usage

```
filemagic.exe <file>
```

## Expected

With `fixtures/sample-a.txt`: `type: text`, exit `0`.
