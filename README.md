# UniClass Classifier para Revit 2024

Plugin de classifica��o autom�tica de elementos do Revit usando UniClass 2015 e FuzzySharp.

## Instala��o de Pacotes NuGet

**IMPORTANTE**: Antes de compilar o projeto, instale os seguintes pacotes NuGet:

### Via Visual Studio Package Manager Console:

```powershell
Install-Package Newtonsoft.Json -Version 13.0.3
Install-Package FuzzySharp -Version 2.0.2
```

### Via NuGet Package Manager UI:

1. Clique com bot�o direito no projeto "CoordinatorPro"
2. Selecione "Manage NuGet Packages..."
3. Na aba "Browse", procure e instale:
   - **Newtonsoft.Json** vers�o 13.0.3
   - **FuzzySharp** vers�o 2.0.2

## Estrutura do Projeto

```
CoordinatorPro/
??? Commands/
?   ??? ClassifyCommand.cs         # Comando principal de classifica��o
?   ??? HelloWorldCommand.cs       # Comando de teste
??? Forms/
?   ??? ParameterSelectionForm.cs  # Sele��o de par�metro destino
?   ??? ProgressForm.cs # Tela de progresso
??? Models/
?   ??? UniClassModels.cs       # Modelos de dados
??? Services/
?   ??? ClassificationService.cs   # L�gica de classifica��o fuzzy
?   ??? ParameterService.cs        # Manipula��o de par�metros
??? Utils/
?   ??? SettingsManager.cs # Gerenciamento de configura��es
??? Config/ # Pasta para prefer�ncias do usu�rio
??? uniclass2015.json             # Base de dados UniClass

```

## Funcionalidades

### 1. Sele��o de Par�metro
- Escolha qual par�metro receber� a classifica��o
- Op��o de lembrar a escolha
- Suporta par�metros de Inst�ncia e Tipo

### 2. Classifica��o Inteligente
- Usa FuzzySharp para matching fuzzy
- Cache de resultados para melhor performance
- Peso diferenciado para Categoria, Family e Type

### 3. Interface de Progresso
- Acompanhamento em tempo real
- Exibi��o de confian�a (%) para cada elemento
- Exporta��o de log em CSV

### 4. Configura��es Persistentes
- Salva prefer�ncias em `%AppData%\UniClassClassifier\settings.json`
- Lembra �ltimo par�metro selecionado
- Configura��o de confian�a m�nima

## Como Usar

1. Abra um projeto no Revit 2024
2. Selecione os elementos que deseja classificar (opcional)
3. Execute o comando "UniClass 2015"
4. Selecione o par�metro destino
5. Aguarde a classifica��o
6. Revise os resultados

## Configura��o do uniclass2015.json

O arquivo `uniclass2015.json` deve ter o seguinte formato:

```json
[
  {
    "Code": "Pr_20_10_10",
    "Title": "Paredes externas",
    "Parent": "Pr_20_10",
    "Description": "Paredes externas de edifica��es"
  },
  ...
]
```

### Build Action: Embedded Resource

Certifique-se de que `uniclass2015.json` est� configurado como **Embedded Resource** nas propriedades do arquivo.

## Deploy

### Build Event

O projeto est� configurado para copiar automaticamente para:
```
C:\ProgramData\Autodesk\Revit\Addins\2024\
```

### Arquivos Necess�rios

- CoordinatorPro.dll
- UniClassClassifier.addin (ou CoordinatorPro.addin)
- FuzzySharp.dll
- Newtonsoft.Json.dll
- uniclass2015.json

## Troubleshooting

### Erro: "FuzzySharp n�o pode ser encontrado"
- Instale o pacote via NuGet Package Manager
- Verifique se o packages.config inclui FuzzySharp 2.0.2
- Recompile o projeto

### Erro: "uniclass2015.json n�o encontrado"
- Verifique se o arquivo est� na raiz do projeto
- Certifique-se de que Build Action = Embedded Resource
- Ou coloque o arquivo na mesma pasta da DLL compilada

### Classifica��o com baixa confian�a
- Verifique se a base UniClass est� completa
- Adicione mais dados aos elementos (Family, Type, Description)
- Ajuste os pesos no ClassificationService.cs

## Desenvolvimento

### Adicionar Novos Comandos

1. Crie uma classe em `Commands/`
2. Implemente `IExternalCommand`
3. Adicione ao `CoordinatorProApp.cs` no m�todo `OnStartup`

### Personalizar Classifica��o

Edite `ClassificationService.cs`:
- Ajuste pesos de busca (linhas 85-95)
- Modifique cutoff do fuzzy matching (linha 133)
- Implemente l�gica de aprendizado

## Licen�a

Projeto desenvolvido para uso interno.

## Contato

Para suporte, entre em contato com a equipe de desenvolvimento.
