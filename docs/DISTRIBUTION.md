# Distribuir o Copilume

## Entrega atual

O DMG contém o aplicativo universal, um atalho para Applications, instruções e licença. Não executa instaladores privilegiados nem instala agentes auxiliares. A assinatura atual é ad hoc, adequada para o build local e inspeção de integridade. Ela não oferece a identidade de um certificado Developer ID.

Nenhuma identidade de assinatura válida estava disponível no chaveiro deste Mac durante a preparação. Por isso, a entrega **não está notarizada**. Não é possível prometer abertura sem avisos em Macs de terceiros. Apple Silicon foi testado neste Mac; a cobertura Intel está descrita em TESTING.md.

## GitHub

O repositório oficial é [kushhoficial/Copilume](https://github.com/kushhoficial/Copilume). Código, assets, documentação e testes são versionados. `build/`, `dist/`, históricos pessoais e credenciais ficam fora do Git. Instaladores são publicados como assets de Release, não como arquivos do histórico Git.

A CI usa `macos-26` com Command Line Tools/SDK 27, Actions oficiais fixadas por commit e permissões de leitura. A etapa de Release recebe escrita em conteúdo somente quando uma tag de versão é enviada. A chave SSH deste Mac não é disponibilizada à CI; a publicação usa o `GITHUB_TOKEN` temporário fornecido pelo GitHub.

`VERSION` define a versão pública e `BUILD_NUMBER` o contador de build. O workflow confere se a tag `vX.Y.Z` corresponde à versão do `.app` antes de publicar. O pacote continua ad hoc enquanto não houver Developer ID e notarização. Consulte [GITHUB.md](GITHUB.md) para o fluxo de manutenção.

## Versão notarizada

Para a experiência usual de abrir um aplicativo baixado, o mantenedor precisa fornecer uma identidade **Developer ID Application** e acesso ao serviço de notarização Apple. O app pode continuar gratuito; a participação no programa Apple é separada da licença do projeto.

Com o certificado já instalado e autorizado no chaveiro, use o suporte existente do build:

```bash
SIGN_IDENTITY="Developer ID Application: NOME (TEAMID)" ./build.sh
./package.sh
```

O build usa hardened runtime e timestamp de assinatura para identidades não ad hoc. Depois, com um perfil `notarytool` previamente configurado no chaveiro pelo mantenedor:

```bash
xcrun notarytool submit dist/Copilume-1.3.0-universal.dmg --keychain-profile Copilume-notary --wait
xcrun stapler staple dist/Copilume-1.3.0-universal.dmg
xcrun stapler validate dist/Copilume-1.3.0-universal.dmg
spctl --assess --type open --context context:primary-signature --verbose dist/Copilume-1.3.0-universal.dmg
(cd dist && shasum -a 256 Copilume-1.3.0-universal.dmg > SHA256SUMS.txt)
```

Esses comandos são instruções para o mantenedor; não foram executados com credenciais nem enviaram o aplicativo à Apple nesta entrega. Não coloque senhas no código, na linha de comando ou no repositório. Um envio ao serviço Apple é uma etapa externa a ser executada quando houver autorização e credenciais apropriadas.

Antes de publicar, teste o download com quarentena em um Mac separado, o pedido de Acessibilidade, login real, versões mínimas do sistema, Intel físico e layouts de teclado diferentes. Evite anunciar “100% em qualquer Mac”: a colagem depende do destino e das permissões do sistema.

Referências: [Developer ID](https://developer.apple.com/developer-id/) e [Notarização](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution).
