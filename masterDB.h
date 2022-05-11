#include <stdio.h>
#include <fstream>
#include <winreg.h>
#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <shellapi.h>
#include <string>
#include <iostream>
#include <sstream>
#include <algorithm>
#include <filesystem>

#include "main.h"

using namespace std;

#define DB_BUFSIZ 1028

class FileDB{
    public:
        string domainName = "bf1942.eu";
        string IP;
        int port = 28901;
        int status = 0;
        int serverVersion = 0;
        FileDB(const char* domainNameReplace = 0, const char* IPReplace = 0);
        int init();
        void changeDomainName(const char* domainNameReplace );
        int connect();
        int send(const char* data);
        int receive();
        int disconnect();
        int awknowlage();
        int handshake(const char* keyhash); //send and receive versions. master also tells if its compatible
        int checkIfMapExists(const char* map, const char* modID);
        int downloadMap(const string& map, const string& modID);
    private:
        SOCKET socket_tcp = INVALID_SOCKET;
        WSADATA wsaData;
        char recvBuf[DB_BUFSIZ];
        int recvDataSize = 0;
};
