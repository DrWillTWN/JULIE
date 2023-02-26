# JULIE
materials for chatbot JULIE
1) appData/5F_Building.accdb: MS Access database for demonstrative building model exported by Revit
2) appData/f1score.xlsx: training data and testing summary
3) Controllers/LinebotJulieController.cs: Linebot JULIE

Note:
To execute the LINEBOT, you have to:
  -Register a LINE account, and create a channel for MESSAGING API
  -Write down your USER ID and Channel Access Token
  -Sign in a Microsoft account and register Azure Cognitive Services for Language to create a Conversational Language Understanding (CLU) project.
  -Write down the key and endpoint of your CLU project. 
  -Create a NLU model accounting to [Will Y. Lin, Prototyping a Chatbot for Site Managers Using Building Information Modeling (BIM) and Natural Language Understanding (NLU) Techniques, Sensors]
  -Write down your project name and deployment name
  -replace those [] with yours in Controllers/LinebotJulieController.cs
