# Manter o Copilume no GitHub

Repositório oficial: https://github.com/kushhoficial/Copilume

## Atualizações do código

Depois de implementar e revisar uma mudança:

```bash
git pull --ff-only
./test.sh
./build.sh
git status --short
git add Sources Tests README.md CHANGELOG.md
# Ajuste a lista aos arquivos realmente alterados.
git diff --cached --check
git diff --cached
git commit -m "Descreva a mudança concreta"
git push origin main
```

Não use `git push --force` no fluxo normal. Em caso de mudanças remotas, integre-as e verifique novamente antes de enviar. Commits devem representar alterações concluídas e testadas; não um envio automático de qualquer arquivo salvo. Solicitações de terceiros podem ser revisadas por pull request.

## Publicar uma versão

1. Atualize `VERSION`, incremente `BUILD_NUMBER` e escreva CHANGELOG e `docs/RELEASE-NOTES.md`.
2. Teste e compile; confira o funcionamento em um documento descartável.
3. Revise e envie o commit para `main`; confira a CI.
4. Crie e envie a tag correspondente:

```bash
VERSION="$(cat VERSION)"
git tag -a "v$VERSION" -m "Copilume $VERSION"
git push origin "v$VERSION"
```

A CI valida a tag, testa, compila, empacota e publica a Release com DMG e SHA-256. `build/` e `dist/` permanecem fora do Git. Nunca mova uma tag de versão já publicada para outro commit; crie uma nova versão.

## Conexão deste Mac

O remote `origin` usa o alias SSH `github-copilume`, com uma chave exclusiva do projeto. A chave privada fica em `~/.ssh/copilume_github_ed25519`, fora do repositório e com modo 0600. A chave pública pode ser cadastrada como deploy key com escrita **somente** no repositório Copilume.

Essa chave não tem senha para permitir os envios autorizados neste Mac, e não expira automaticamente. Proteja a conta local. Para revogar, remova a deploy key em Settings → Deploy keys no GitHub. A chave não dá acesso aos demais repositórios da conta.

Em outra máquina, configure sua própria autenticação GitHub; não copie a chave privada deste Mac para o projeto nem para a CI. O endereço padrão de clone é `git@github.com:kushhoficial/Copilume.git` ou `https://github.com/kushhoficial/Copilume.git`.

A chave SSH autoriza operações Git, não autentica a API de Releases. O workflow usa um token temporário do GitHub, limitado ao job de publicação.
