using System;
using System.Linq;
using System.Threading;
using System.Text.Json;
using System.Data;
using System.Data.Odbc;
using Microsoft.AspNetCore.Mvc;
using Azure;
using Azure.Core;
using Azure.AI.Language.Conversations;

namespace isRock.Template
{
    public class LinebotJulieController : isRock.LineBot.LineWebHookControllerBase
    {
        [Route("api/CJULIE")]
        [HttpPost]
        public IActionResult POST()
        {
            var AdminUserId = "[YOUR_LINE_USERID]";
            try
            {
                this.ChannelAccessToken = "[YOUR_LINE_CHANNEL_ACCESS_TOKEN]]";
                if (ReceivedMessage.events == null || ReceivedMessage.events.Count() <= 0 ||
                    ReceivedMessage.events.FirstOrDefault().replyToken == "00000000000000000000000000000000") return Ok();
                var LineEvent = this.ReceivedMessage.events.FirstOrDefault();
                var responseMsg = "";
                if (LineEvent.type.ToLower() == "message" && LineEvent.message.type == "text")
                {
                    String mytext = Convert.ToString(LineEvent.message.text);
                    using JsonDocument result = JsonDocument.Parse(LUISMakeRequest(mytext));
                    JsonElement conversationalTaskResult = result.RootElement;
                    string topIntent = conversationalTaskResult.GetProperty("topIntent").GetString();
                    responseMsg = $"Your Inquiry: {mytext}\n";
                    var otherIntent = "";
                    var topIntentConfidence=0.0;
                   foreach (JsonElement intent in conversationalTaskResult.GetProperty("intents").EnumerateArray())
                    {
                        if (intent.GetProperty("category").GetString()==topIntent)
                        {
                            topIntentConfidence = intent.GetProperty("confidenceScore").GetSingle();
                            responseMsg += String.Format("TopIntent: {0} ({1:0.000}%)\n",topIntent,topIntentConfidence*100);
                        }
                        else
                        {
                            otherIntent += String.Format(" -{0}({1:0.000}%)\n",intent.GetProperty("category").GetString(),intent.GetProperty("confidenceScore").GetSingle()*100);
                        }
                    }
                    responseMsg += $"Entities: \n";
                    JsonElement category;
                    foreach (JsonElement entity in conversationalTaskResult.GetProperty("entities").EnumerateArray())
                    {
                        if (entity.TryGetProperty("category", out category))
                        {
                            responseMsg += " -" +category.GetString() + ": " + entity.GetProperty("text").GetString() +"\n";
                        }
                    }
                    responseMsg += $"Other Intents: \n" + otherIntent;
                    if(topIntentConfidence>0.7 && topIntent!="None")
                    {
                        if (topIntent=="Inquiry-FloorHeight")
                        {
                            JsonElement value;
                            string FloorName=null,LengthUnit;
                            foreach (JsonElement entity in conversationalTaskResult.GetProperty("entities").EnumerateArray())
                            {
                                if (entity.TryGetProperty("category", out value) && value.GetString()=="FloorName")
                                    FloorName = entity.GetProperty("text").GetString().ToUpper();
                                if (entity.TryGetProperty("category", out value) && value.GetString()=="LengthUnit")
                                    LengthUnit = entity.GetProperty("text").GetString().ToUpper();
                            }
                            //determine responseMsg
                            if (FloorName==null)
                                responseMsg = $"Floor name is not found. Please specify a floor name."+ "\n\n"+ responseMsg;
                            else{
                                FloorName = formatFloorNumber(FloorName);
                                var SQL = "SELECT FloorHeight FROM InquiryFloorHeight WHERE ThisStory = '" + FloorName + "'";
                                string queryResult = makeDBConnection(SQL);
                                if (IsNumeric(queryResult))
                                    responseMsg = Math.Round(Convert.ToDouble(queryResult),2, MidpointRounding.AwayFromZero) + "m\n\n"+ responseMsg;
                                else
                                    responseMsg = "Insufficient Paramenters: " + queryResult + "\n\n"+ responseMsg;
                            }
                        }
                        else if (topIntent=="Inquiry-ColumnSize")
                        {
                            JsonElement value;
                            string SQL_WHERE="";
                            string FloorName=null,LengthUnit=null,ColumnLabel=null,Grid_X=null,Grid_Y=null;
                            foreach (JsonElement entity in conversationalTaskResult.GetProperty("entities").EnumerateArray())
                            {
                                if (entity.TryGetProperty("category", out value) && value.GetString()=="FloorName")
                                    FloorName = entity.GetProperty("text").GetString().ToUpper();
                                if (entity.TryGetProperty("category", out value) && value.GetString()=="LengthUnit")
                                    LengthUnit = entity.GetProperty("text").GetString().ToUpper();
                                if (entity.TryGetProperty("category", out value) && value.GetString()=="ColumnLabel")
                                    ColumnLabel = entity.GetProperty("text").GetString().ToUpper();
                                if (entity.TryGetProperty("category", out value) && value.GetString()=="Grid-X")
                                    Grid_X = entity.GetProperty("text").GetString().ToUpper();
                                if (entity.TryGetProperty("category", out value) && value.GetString()=="Grid-Y")
                                    Grid_Y = entity.GetProperty("text").GetString().ToUpper();
                            }
                            //determine responseMsg
                            if (FloorName==null)
                                responseMsg = $"Floor name is not found. Please specify a floor name."+ "\n\n"+ responseMsg;
                            else
                            {
                                FloorName = formatFloorNumber(FloorName);
                                SQL_WHERE = "SELECT SectionLength \nFROM InquiryColumnSize \nWHERE " + String.Format("BaseLevel='{0}' \n",FloorName);
                                if(ColumnLabel!=null)//named method
                                    SQL_WHERE += String.Format("AND Mark='{0}' \n",ColumnLabel);
                                else if(Grid_X!=null && Grid_Y!=null)//grid method
                                    SQL_WHERE += String.Format("AND ((GridX='{0}' AND GridY='{1}') OR (GridX='{1}' AND GridY='{0}')) \n",Grid_X.Substring(5),Grid_Y.Substring(5));
                                string queryResult = makeDBConnection(SQL_WHERE);

                                if (IsNumeric(queryResult))
                                    responseMsg = Math.Round(Convert.ToDouble(queryResult),2, MidpointRounding.AwayFromZero) + "m\n\n"+ responseMsg;
                                else
                                    responseMsg = "Insufficient Paramenters: " + queryResult + "\n\n"+ responseMsg;
                            }
                        }
                    }
                    else
                        responseMsg = $"I'm not sure what you mean. Please put it another way." + "\n\n"+ responseMsg;
                    this.ReplyMessage(LineEvent.replyToken, responseMsg);
                }
                else
                {
                    responseMsg = $"Received event : {LineEvent.type} ";
                    this.ReplyMessage(LineEvent.replyToken, responseMsg);
                }
                return Ok();
            }
            catch (Exception ex)
            {
                this.PushMessage(AdminUserId, "Error:\n" + ex.Message);
                return Ok();
            }
        }

        public static bool IsNumeric(string text)
        {
            double test;
            return double.TryParse(text, out test);
        }

        public string formatFloorNumber(string fn)
        {
            switch (fn)
            {
                case "1F":
                case "1FL":                
                case "1ST FLOOR":
                case "FIRST FLOOR":
                case "Level 1":
                    return "1FL";
                case "2F":
                case "2FL":                
                case "2ND FLOOR":
                case "SECOND FLOOR":
                case "Level 2":                
                    return "2FL";
                case "3F":
                case "3FL":                
                case "3RD FLOOR":
                case "THIRD FLOOR":
                case "Level 3":                
                    return "3FL";
                case "4F":
                case "4FL":                
                case "4TH FLOOR":
                case "FOURTH FLOOR":
                case "Level 4":    
                    return "4FL";
                case "5F":
                case "5FL":                    
                case "5TH FLOOR":
                case "FIFTH FLOOR":
                case "Level 5":                
                    return "5FL";
                case "RF":
                case "RFL":                      
                case "ROOF FLOOR":
                case "TOP FLOOR":
                case "TOP Level":                
                    return "RFL";
                case "P2 FLOOR":
                case "Level P2":
                case "P2":                                
                    return "P2";
                case "P3 FLOOR":
                case "P3":
                case "Level P3":                
                    return "P3";
                default:
                    return "3FL";
            }
        }

        const string keyLUIS = "[YOUR_LUIS_KEY]";
        const string endpointLUIS = "[YOUR_LUIS_ENDPOINT]";
        static string makeDBConnection(string SQL)
        {
            string connectionString = "Driver={Microsoft Access Driver (*.mdb, *.accdb)};Dbq=[YOUR_PATH]\\5F_Building.accdb;";
            using (OdbcConnection connection = new OdbcConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    string r = "";
                    using (OdbcCommand command = new OdbcCommand(SQL, connection))
                    {
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                r += reader[0];
                            }
                        }
                    }
                    connection.Close();
                    return r;
                }
                catch (OdbcException ex)
                {
                    return SQL + "\n" + ex.Message;
                }
            }
        }
        
        static string LUISMakeRequest(string utterance)
        {
            Uri endpoint = new Uri(endpointLUIS);
            AzureKeyCredential credential = new AzureKeyCredential(keyLUIS);
            ConversationAnalysisClient client = new ConversationAnalysisClient(endpoint, credential);
            string projectName = "[YOUR_PROJECT_NAME]";
            string deploymentName = "[YOUR_DEPLOYMENT_NAME]";
            var data = new
            {
                analysisInput = new
                {
                    conversationItem = new
                    {
                        text = utterance,
                        id = "1",
                        participantId = "1",
                    }
                },
                parameters = new
                {
                    projectName,
                    deploymentName,
                    stringIndexType = "Utf16CodeUnit",
                },
                kind = "Conversation",
            };
            Response response = client.AnalyzeConversation(RequestContent.Create(data));
            using JsonDocument result = JsonDocument.Parse(response.ContentStream);
            JsonElement conversationalTaskResult = result.RootElement;
            JsonElement orchestrationPrediction = conversationalTaskResult.GetProperty("result").GetProperty("prediction");
            return orchestrationPrediction.ToString();
        }
    }
}