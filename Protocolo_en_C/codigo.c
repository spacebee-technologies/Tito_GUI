/******************************************************************************

Modificar linea 121

*******************************************************************************/

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>
#include <string.h>


#define MESSAGE_HANDLER_AREA_VERSION 1              //Version del protocolo
#define MESSAGE_HANDLER_CRC_SIZE 2                  //Tamaño del CRC segun algortimo
#define MAXIMUM_BUFFER_SIZE 512                     //Maximo tamaño del buffer de datos

//Enumeracion y definiciones de los tipos de interaccion en el protocolo
typedef enum MessageHeaderInteractionType {
  MESSAGE_HEADER_INTERACTION_TYPE_SEND = 1,
  MESSAGE_HEADER_INTERACTION_TYPE_SUBMIT = 2,
  MESSAGE_HEADER_INTERACTION_TYPE_REQUEST = 3,
  MESSAGE_HEADER_INTERACTION_TYPE_PUBSUB = 6
} MessageHeaderInteractionType_t;

//Enumeracion y definiciones de los tipos de servicio en el protocolo (Determina si es TM o TC)
typedef enum MessageHeaderService {
  MESSAGE_HEADER_SERVICE_TELEMETRY,
  MESSAGE_HEADER_SERVICE_TELECOMMAND
} MessageHeaderService_t;

//Estructura del encabeado o header del protocolo
typedef struct __attribute__((packed)) MessageHeader {
  uint64_t timestamp;                       //Marca de tiempo: Tiempo en milisegundos
  uint16_t interactionType;                 //Tipo de interacción: 1 para ENVIAR, 2 para ENVIAR, 3 para SOLICITAR y 6 para PUBSUB (según CCSDS 521.0-B-2, Sección 4.4.1)
  uint8_t interactionStage;                 //Etapa de interacción: 1 o 2 según el orden del mensaje
  uint64_t transactionId;                   //ID de transacción: Identificador incremental único
  uint16_t service;                         //Servicio: 0 para telemetrías (TM) o 1 para telecomandos (TC)
  uint16_t operation;                       //Operación: Identificador único para una telemetría o telecomando dado
  uint16_t areaVersion;                     //Versión del encabezado: Versión del protocolo
  uint8_t isErrorMessage;                   //Indica si es un mensaje de error: Valor booleano para indicar si es un mensaje de error (0x1 para verdadero, 0x0 para falso)
  uint16_t bodyLength;                      //Longitud del cuerpo: Longitud en bytes del cuerpo del mensaje
} MessageHeader_t;

/*Estructura del mensaje completo: Header+body+crc
+--------------+---------------------+-------------+
| Header       | Body                | CRC         |
+--------------+---------------------+-------------+
|<- 224 bits ->|<- Variable length ->|<- 16 bits ->|
*/
typedef struct Message {
  MessageHeader_t header;
  uint8_t *body;
  uint8_t crc[MESSAGE_HANDLER_CRC_SIZE];
} Message_t;

//Creo dos objetos del tipo mensaje. Uno para recepcion y otro para transmicion
Message_t message_rx;
Message_t message_tx;

//Prototipos de funciones
bool MessageHandler_isTelecommand(Message_t *message);
void MessageHandler_setOperation(Message_t *message, uint16_t operation);
void MessageHandler_setHeader(Message_t *message, MessageHeader_t header);
static bool MessageHandler_parse(Message_t *message, const char *buffer, size_t bufferLength);
static void MessageHandler_parseCrc(Message_t *message, const char *buffer, size_t bufferLength);
static void MessageHandler_parseBody(Message_t *message, const char *buffer, size_t bufferLength);
static void MessageHandler_parseHeader(Message_t *message, const char *buffer);


/*========================================================================
  Funcion: crc16
  Descripcion: Genera el codigo CRC16 utilizando polinomio normal 0x1021
  Parametro de entrada:  const uint8_t *buffer          = Buffer al cual se quiere generar el CRC
                         size_t size        = Tamaño del buffer
  Retortna:
			crc: Valor de CRC obtenido.
  ========================================================================*/
uint16_t crc16(const uint8_t *buffer, size_t size) {
    uint16_t crc = 0xFFFF;
    for (size_t i = 0; i < size; ++i) {
        crc ^= (uint16_t)buffer[i] << 8;
        for (size_t j = 0; j < 8; ++j) {
            if (crc & 0x8000) {
                crc = (crc << 1) ^ 0x1021;
            } else {
                crc <<= 1;
            }
        }
    }
    return crc;
}

/*========================================================================
  Funcion: MessageHandler_initializeHeader
  Descripcion: Inicializa el encabezado o header del mensaje
  Parametro de entrada:  Message_t *message             = Puntero al objeto que se desea inicializar el encabezado
                         uint8_t interactionType        = Indica el tipo de interaccion 1 para ENVIAR, 2 para ENVIAR, 3 para SOLICITAR y 6 para PUBSUB (según CCSDS 521.0-B-2, Sección 4.4.1)
                         uint8_t interactionStage       = Etapa de interacción: 1 o 2 según el orden del mensaje
                         MessageHeaderService_t service = 0 para telemetrías (TM) o 1 para telecomandos (TC)
                         bool isErrorMessage            = Indica si es un mensaje de error: Valor booleano para indicar si es un mensaje de error (0x1 para verdadero, 0x0 para falso)
  No retorna nada
  ========================================================================*/
void MessageHandler_initializeHeader(Message_t *message, uint8_t interactionType, uint8_t interactionStage, MessageHeaderService_t service, bool isErrorMessage) {
  message->header.interactionType = interactionType;
  message->header.interactionStage = interactionStage;
  message->header.transactionId = 0;                             //Establece el id de transicion en 0
  message->header.service = service;
  message->header.areaVersion = MESSAGE_HANDLER_AREA_VERSION;    //Indica la version del protocolo
  message->header.isErrorMessage = isErrorMessage;
}

/*========================================================================
  Funcion: MessageHandler_send
  Descripcion: Envia mensaje por UDP con el protocolo
  Parametro de entrada:  Message_t *message             = Puntero al objeto que se desea enviar
                         char *messageBody              = Cuerpo o mensaje que se desea enviar
                         uint16_t messageBodySize       = Tamaño del mensaje que se quiere enviar
  No retorna nada
  ========================================================================*/
void MessageHandler_send(Message_t *message, char *messageBody, uint16_t messageBodySize) {
  message->header.timestamp = (uint64_t)10;															//MODIFICAR!!!!!!!
  message->header.bodyLength = messageBodySize;
  size_t messageFullSize = sizeof(MessageHeader_t) + messageBodySize + MESSAGE_HANDLER_CRC_SIZE;
  char buffer[messageFullSize];
  memcpy(buffer, &message->header, sizeof(MessageHeader_t));
  memcpy(buffer + sizeof(MessageHeader_t), messageBody, messageBodySize);
  char crc[MESSAGE_HANDLER_CRC_SIZE] = {0};  // TODO: Get the correct CRC calculation
  size_t tamanio= sizeof(MessageHeader_t) + messageBodySize;
  uint16_t crc2=crc16((const uint8_t*)buffer, tamanio);
  crc[1]=(char) (crc2 >> 8);
  crc[0]=(char) (crc2 & 0xFF);
  memcpy(buffer + sizeof(MessageHeader_t) + messageBodySize, crc, MESSAGE_HANDLER_CRC_SIZE);
  message->header.transactionId++;
  //UdpHandler_send(self->udpHandler, buffer, messageFullSize);
}

/*========================================================================
  Funcion: MessageHandler_parseHeader
  Descripcion: Parsea el buffer para obtener los datos del header y almacenarlo en los diferentes campos del objeto
  Parametro de entrada:  Message_t *message             = Puntero al objeto
                         const char *buffer             = Buffer que se desea parsear
  No retorna nada
  ========================================================================*/
static void MessageHandler_parseHeader(Message_t *message, const char *buffer) {
  size_t memoryOffset = 0;                                                                          
  memcpy(&message->header.timestamp, buffer + memoryOffset, sizeof(message->header.timestamp));
  memoryOffset += sizeof(message->header.timestamp);
  memcpy(&message->header.interactionType, buffer + memoryOffset, sizeof(message->header.interactionType));
  memoryOffset += sizeof(message->header.interactionType);
  memcpy(&message->header.interactionStage, buffer + memoryOffset, sizeof(message->header.interactionStage));
  memoryOffset += sizeof(message->header.interactionStage);
  memcpy(&message->header.transactionId, buffer + memoryOffset, sizeof(message->header.transactionId));
  memoryOffset += sizeof(message->header.transactionId);
  memcpy(&message->header.service, buffer + memoryOffset, sizeof(message->header.service));
  memoryOffset += sizeof(message->header.service);
  memcpy(&message->header.operation, buffer + memoryOffset, sizeof(message->header.operation));
  memoryOffset += sizeof(message->header.operation);
  memcpy(&message->header.areaVersion, buffer + memoryOffset, sizeof(message->header.areaVersion));
  memoryOffset += sizeof(message->header.areaVersion);
  memcpy(&message->header.isErrorMessage, buffer + memoryOffset, sizeof(message->header.isErrorMessage));
  memoryOffset += sizeof(message->header.isErrorMessage);
  memcpy(&message->header.bodyLength, buffer + memoryOffset, sizeof(message->header.bodyLength));
  memoryOffset += sizeof(message->header.bodyLength);
}

/*========================================================================
  Funcion: MessageHandler_parseBody
  Descripcion: Parsea el buffer para obtener los datos del body y almacenarlo en el campo body del objeto
  Parametro de entrada:  Message_t *message             = Puntero al objeto
                         const char *buffer             = Buffer que se desea parsear
                         size_t bufferLength            = Tamaño del buffer
  No retorna nada
  ========================================================================*/
static void MessageHandler_parseBody(Message_t *message, const char *buffer, size_t bufferLength) {
  size_t bodySize = bufferLength - sizeof(MessageHeader_t) - MESSAGE_HANDLER_CRC_SIZE;
  message->body = (uint8_t *)malloc(bodySize);
  memcpy(message->body, buffer + sizeof(MessageHeader_t), bodySize);
}

/*========================================================================
  Funcion: MessageHandler_parseCrc
  Descripcion: Parsea el buffer para obtener los datos del crc y almacenarlo en el campo crc del objeto
  Parametro de entrada:  Message_t *message             = Puntero al objeto
                         const char *buffer             = Buffer que se desea parsear
                         size_t bufferLength            = Tamaño del buffer
  No retorna nada
  ========================================================================*/
static void MessageHandler_parseCrc(Message_t *message, const char *buffer, size_t bufferLength) {
  memcpy(message->crc, buffer + bufferLength - MESSAGE_HANDLER_CRC_SIZE, MESSAGE_HANDLER_CRC_SIZE);
}

/*========================================================================
  Funcion: MessageHandler_parse
  Descripcion: Parsea el buffer y almacena los datos en los campos correspondiente del objeto
  Parametro de entrada:  Message_t *message             = Puntero al objeto
                         const char *buffer             = Puntero al buffer que se desea parsear
                         size_t bufferLength            = Tamaño del buffer
  Retorna:  
            False: Si no se pudo realizar 
            True:  Si se pudo realizar o si fallo la comprobacion de CRC
  ========================================================================*/
static bool MessageHandler_parse(Message_t *message, const char *buffer, size_t bufferLength) {
  size_t minMessageLength = sizeof(MessageHeader_t) + MESSAGE_HANDLER_CRC_SIZE;                     //Tamaño minimo del mensaje
  if (bufferLength < minMessageLength) { return false; }                                            //Si el mensaje posee menos bytes, se retorna falso
  
  MessageHandler_parseHeader(message, buffer);                                                      //Se parsea el header
  uint16_t crc2=crc16((const uint8_t*)buffer, bufferLength-MESSAGE_HANDLER_CRC_SIZE);				//Se calcula el CRC
  
  size_t expectedMessageLength = sizeof(MessageHeader_t) + message->header.bodyLength + MESSAGE_HANDLER_CRC_SIZE; //Obtengo el tamaño que deberia tener el mensaje (tamaño header+tamaño que se indica que es el mensaje+ tamaño crc)
  if (bufferLength != expectedMessageLength) { return false; }                                      //Si el tamaño del mensaje es diferente del que se espera, se devuelve false
  MessageHandler_parseBody(message, buffer, bufferLength);
  MessageHandler_parseCrc(message, buffer, bufferLength);
  if((message->crc[1]==(crc2 >> 8))  && (message->crc[0]==(crc2 & 0xFF))){
	  return true;
  }else{
	  return false;
  }
}

/*========================================================================
  Funcion: MessageHandler_setHeader
  Descripcion: Setea el header de un objeto
  Parametro de entrada:  Message_t *message             = Puntero al objeto
                         MessageHeader_t heade          = Header a setear
  No retorna nada
  ========================================================================*/
void MessageHandler_setHeader(Message_t *message, MessageHeader_t header) {
  message->header = header;
}

/*========================================================================
  Funcion: MessageHandler_setOperation
  Descripcion: Setea la operacion a realizar en el header del objeto
  Parametro de entrada:  Message_t *message             = Puntero al objeto
                         uint16_t operation             = Oeracion a realizar
  No retorna nada
  ========================================================================*/
void MessageHandler_setOperation(Message_t *message, uint16_t operation) {
  message->header.operation = operation;
}

/*========================================================================
  Funcion: MessageHandler_isTelecommand
  Descripcion: Setea el servicio como telecomando en el header del objeto
  Parametro de entrada:  Message_t *message             = Puntero al objeto
  No retorna nada
  ========================================================================*/
bool MessageHandler_isTelecommand(Message_t *message) {
  return message->header.service == MESSAGE_HEADER_SERVICE_TELECOMMAND;
}



bool MessageHandler_receive(Message_t *message) {

  uint8_t buffer[MAXIMUM_BUFFER_SIZE];
  size_t receivedSize;
  //bool isPacketReceived = UdpHandler_receive(self->udpHandler, buffer, MAXIMUM_BUFFER_SIZE, &receivedSize);
  bool isCorrectMessage = MessageHandler_parse(&message_rx, (char *) buffer, receivedSize);
  if (isCorrectMessage) {           //Si no hay error de tamaño en el parceo
    //Aca se debe verificar CRC
    printf("OK \n"); 
    printf("%s \n", message_rx.body); 
  }else{
     printf("Error \n"); 
  }
  //for(int i=0; i<messageFullSize; i++){
  //    printf("%x", buffer[i]);
  //}
}


int main(){
    printf("inicio\n");
    MessageHandler_initializeHeader(&message_tx, MESSAGE_HEADER_INTERACTION_TYPE_SEND, 1, MESSAGE_HEADER_SERVICE_TELECOMMAND, false);
    
    char *message = "Hello";
    uint16_t operation = 0xBEAF;
    MessageHandler_setOperation(&message_tx, operation);
    MessageHandler_send(&message_tx, message, strlen(message));
}
