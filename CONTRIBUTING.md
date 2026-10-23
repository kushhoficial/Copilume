# Contribuir

Abra uma issue ou pull request com o problema concreto, comportamento esperado, versão do macOS e passos de reprodução usando textos fictícios. Não inclua conteúdo real do clipboard, senhas ou arquivos pessoais.

Use Swift/AppKit e APIs públicas, mantenha o aplicativo local e sem telemetria. Preserve as opções de privacidade desativadas por padrão. Execute `./test.sh` e `./build.sh`; confira a interface clara/escura e o fluxo sem Acessibilidade. Testes de colagem devem usar um documento descartável.

No Windows, use C#/WPF com APIs públicas e sem dependências externas de runtime além do .NET. Execute `./test-windows.ps1` e `./build-windows.ps1` com o SDK .NET 10. Consulte [WINDOWS.md](docs/WINDOWS.md) e [WINDOWS-TESTING.md](docs/WINDOWS-TESTING.md). Mantenha clipboard real fora de logs, fixtures e testes automáticos; use o modo `--demo` para inspecionar a interface sem capturar dados pessoais.

Mudanças no binário ad hoc podem exigir conceder Acessibilidade novamente pelo painel oficial. Não adicione comandos que desativem proteções do macOS.
