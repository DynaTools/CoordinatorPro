# UniClass Classifier para Revit 2024

Plugin de classificação automática de elementos do Revit usando UniClass 2015 e FuzzySharp.

## Instalação de Pacotes NuGet

**IMPORTANTE**: Antes de compilar o projeto, instale os seguintes pacotes NuGet:

### Via Visual Studio Package Manager Console:

```powershell
Install-Package Newtonsoft.Json -Version 13.0.3
Install-Package FuzzySharp -Version 2.0.2
```

### Via NuGet Package Manager UI:

1. Clique com botão direito no projeto "CoordinatorPro"
2. Selecione "Manage NuGet Packages..."
3. Na aba "Browse", procure e instale:
   - **Newtonsoft.Json** versão 13.0.3
   - **FuzzySharp** versão 2.0.2

## Estrutura do Projeto

```
CoordinatorPro/
??? Commands/
?   ??? ClassifyCommand.cs         # Comando principal de classificação
?   ??? HelloWorldCommand.cs       # Comando de teste
??? Forms/
?   ??? ParameterSelectionForm.cs  # Seleção de parâmetro destino
?   ??? ProgressForm.cs # Tela de progresso
??? Models/
?   ??? UniClassModels.cs       # Modelos de dados
??? Services/
?   ??? ClassificationService.cs   # Lógica de classificação fuzzy
?   ??? ParameterService.cs        # Manipulação de parâmetros
??? Utils/
?   ??? SettingsManager.cs # Gerenciamento de configurações
??? Config/ # Pasta para preferências do usuário
??? uniclass2015.json             # Base de dados UniClass

```

## Funcionalidades

### 1. Seleção de Parâmetro
- Escolha qual parâmetro receberá a classificação
- Opção de lembrar a escolha
- Suporta parâmetros de Instância e Tipo

### 2. Classificação Inteligente
- Usa FuzzySharp para matching fuzzy
- Cache de resultados para melhor performance
- Peso diferenciado para Categoria, Family e Type

### 3. Interface de Progresso
- Acompanhamento em tempo real
- Exibição de confiança (%) para cada elemento
- Exportação de log em CSV

### 4. Configurações Persistentes
- Salva preferências em `%AppData%\UniClassClassifier\settings.json`
- Lembra último parâmetro selecionado
- Configuração de confiança mínima

## Como Usar

1. Abra um projeto no Revit 2024
2. Selecione os elementos que deseja classificar (opcional)
3. Execute o comando "UniClass 2015"
4. Selecione o parâmetro destino
5. Aguarde a classificação
6. Revise os resultados

## Configuração do uniclass2015.json

O arquivo `uniclass2015.json` deve ter o seguinte formato:

```json
[
  {
    "Code": "Pr_20_10_10",
    "Title": "Paredes externas",
    "Parent": "Pr_20_10",
    "Description": "Paredes externas de edificações"
  },
  ...
]
```

### Build Action: Embedded Resource

Certifique-se de que `uniclass2015.json` está configurado como **Embedded Resource** nas propriedades do arquivo.

## Deploy

### Build Event

O projeto está configurado para copiar automaticamente para:
```
C:\ProgramData\Autodesk\Revit\Addins\2024\
```

### Arquivos Necessários

- CoordinatorPro.dll
- UniClassClassifier.addin (ou CoordinatorPro.addin)
- FuzzySharp.dll
- Newtonsoft.Json.dll
- uniclass2015.json

## Troubleshooting

### Erro: "FuzzySharp não pode ser encontrado"
- Instale o pacote via NuGet Package Manager
- Verifique se o packages.config inclui FuzzySharp 2.0.2
- Recompile o projeto

### Erro: "uniclass2015.json não encontrado"
- Verifique se o arquivo está na raiz do projeto
- Certifique-se de que Build Action = Embedded Resource
- Ou coloque o arquivo na mesma pasta da DLL compilada

### Classificação com baixa confiança
- Verifique se a base UniClass está completa
- Adicione mais dados aos elementos (Family, Type, Description)
- Ajuste os pesos no ClassificationService.cs

## Desenvolvimento

### Adicionar Novos Comandos

1. Crie uma classe em `Commands/`
2. Implemente `IExternalCommand`
3. Adicione ao `CoordinatorProApp.cs` no método `OnStartup`

### Personalizar Classificação

Edite `ClassificationService.cs`:
- Ajuste pesos de busca (linhas 85-95)
- Modifique cutoff do fuzzy matching (linha 133)
- Implemente lógica de aprendizado

## Licença

Projeto desenvolvido para uso interno.

## Contato

Para suporte, entre em contato com a equipe de desenvolvimento.
