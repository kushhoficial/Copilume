Copilume 1.4.2 — por **kushhoficial**. Histórico local de textos, links e imagens para **macOS e Windows**.

## Windows — novo

- Aplicativo nativo para Windows 10/11, com **Alt + V** configurável e ícone de bandeja.
- Textos, links e imagens, busca sem acentos, filtros, fixados, prévia e cópia em uma linha.
- Histórico apenas em memória por padrão; persistência opcional criptografada para seu usuário Windows.
- Colagem automática e início no login opcionais, desligados por padrão.
- ZIP portátil x64 ou ARM64 com runtime incluído: extraia todo o pacote e abra `Copilume.exe`.
- Novo acabamento inspirado no Mac: painel em vidro, filtros segmentados, cartões compactos e ícones vetoriais. Preferências, prévia e apresentação seguem a mesma identidade. Acrylic nativo no Windows 11 22H2+, com acabamento compatível no Windows 10.
- Cantos recortados também na janela nativa, moldura consistente ao perder foco e menus de bandeja/itens/texto no mesmo acabamento em vidro. Reflexos respondem ao ponteiro, com navegação por teclado e respeito às preferências visuais do Windows.

Os pacotes Windows iniciais não possuem assinatura Authenticode. Não desative proteções do Windows; a compilação local é documentada em `docs/WINDOWS.md`. A colagem automática respeita o foco e os limites do sistema; destinos elevados/protegidos podem exigir Ctrl + V manual. ARM64 foi compilado, sem execução em hardware ARM64. Consulte `docs/WINDOWS-TESTING.md` para o alcance dos testes.

## macOS

- Abra com **Option + V**, pesquise e escolha com Enter.
- Miniaturas, filtro de imagens, visualização ampliada e itens fixados.
- Histórico somente em memória por padrão; persistência local opcional.
- Sem conta, nuvem, telemetria ou API de IA no aplicativo.

Baixe o **DMG universal**, arraste o aplicativo para **Applications**, ejete o disco e abra a cópia instalada. Saia da versão anterior antes de atualizar. O histórico em memória é perdido ao sair; guarde conteúdos importantes antes disso.

**macOS 13+ · Apple Silicon e Intel.** Liquid Glass nativo em macOS 26+. O binário é universal; a execução em hardware Intel não foi validada.

**Assinatura local ad hoc, sem notarização Apple.** Downloads podem ser bloqueados pelo Gatekeeper. Não desative proteções do macOS; você pode recompilar o código neste repositório. A colagem automática requer Acessibilidade e um destino que aceite o conteúdo. Após uma atualização ad hoc, pode ser necessário readicionar o aplicativo instalado na autorização oficial do macOS.

Confira README, docs/TESTING.md, docs/WINDOWS-TESTING.md e CHANGELOG.md para recursos e limitações. `SHA256SUMS.txt` verifica o DMG; `SHA256SUMS-windows-x64.txt` e `SHA256SUMS-windows-arm64.txt` verificam os ZIPs. Licença MIT.
