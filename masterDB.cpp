#include "masterDB.h"

int strcicmp(char const *a, char const *b){
    int d = 0;
    for (;; a++, b++){
        if (!*a || !*b)
            return d;
        d = tolower((unsigned char)*a) - tolower((unsigned char)*b);
        if (d != 0)
            return d;
    }
}

int fileSetWriteTime(const char *filename, unsigned long long timestamp){
    FILETIME WriteTime;
    HANDLE h = CreateFile(filename, FILE_WRITE_ATTRIBUTES, FILE_SHARE_WRITE, NULL, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, NULL);

    if(h == NULL){
        cout << "Error at CreateFile(): " << GetLastError() << endl;
        return 1;
    }

    timestamp = timestamp * (long long unsigned)10000000 + (long long unsigned)116444736000000000;

    WriteTime.dwHighDateTime = timestamp >> 32 & 0xFFFFFFFF;
    WriteTime.dwLowDateTime = timestamp & 0xFFFFFFFF;
    int success = SetFileTime(h, NULL, NULL, &WriteTime) == 1;
    CloseHandle(h);
    if(!success){
        cout << "Error at SetFileTime(): " << GetLastError() << endl;
        return 0;
    }
    return 1;
}

FileDB::FileDB(const char* domainNameReplace, const char* IPReplace){
    if(domainNameReplace)
        this->domainName = domainNameReplace;
    if(IPReplace)
        this->IP = IPReplace;
}

int FileDB::init(){
    int iResult = WSAStartup(MAKEWORD(2,2), &(this->wsaData));
    if(iResult != 0){
        cout << "WSAStartup failed: " << iResult << endl;
        return 0;
    }
    return 1;
}

void FileDB::changeDomainName(const char* domainNameReplace){
    this->domainName = domainNameReplace;
    this->IP = "";
}

int FileDB::connect(){
    int i = 0, iResult;

    // Create a SOCKET for connecting to server
    this->socket_tcp = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if(this->socket_tcp == INVALID_SOCKET){
        cout << "Error at socket(): " << WSAGetLastError() << endl;
        WSACleanup();
        return 0;
    }

    if(this->IP == ""){
        struct hostent * host_info;
        struct in_addr addr;
        host_info  = gethostbyname(domainName.c_str());
        if (host_info == NULL){
            cout << "Error: can't resolve DNS" << endl;
            closesocket(this->socket_tcp);
            return 0;
        }
//        cout << "Hostname : " << host_info->h_name << endl;
        while(host_info->h_addr_list[i] != 0){
            addr.s_addr = *(u_long *) host_info->h_addr_list[i++];
            this->IP = inet_ntoa(addr);
            // cout << "IP Address " << this->IP << endl; // inet_ntoa function converts IPv4 address to ASCII string in Internet standard dotted-decimal format.
        }
    }

    if(this->IP == ""){
        cout << "Error: can't find IP" << endl;
        closesocket(this->socket_tcp);
        return 0;
    }

    SOCKADDR_IN target;
    target.sin_family = AF_INET;
    target.sin_port = htons(this->port);
    target.sin_addr.s_addr = inet_addr(this->IP.c_str());

    // Connect to server.
    iResult = ::connect(this->socket_tcp, (SOCKADDR *)&target, sizeof(target));
    if (iResult == SOCKET_ERROR) {
        cout << "Error at connect(): " << WSAGetLastError() << endl;
        closesocket(this->socket_tcp);
        this->socket_tcp = INVALID_SOCKET;
        return 0;
    }
    this->status = 1;
    return(1);
}

int FileDB::send(const char* data){
    int iResult;

    iResult = ::send(this->socket_tcp, data, strlen(data), 0);
    if(iResult == SOCKET_ERROR){
        cout << "Error at send(): " << WSAGetLastError() << endl;
        closesocket(this->socket_tcp);
        WSACleanup();
        return 0;
    }
    return(1);
}

int FileDB::receive(){ //one packet
    int iResult;
    iResult = recv(this->socket_tcp, this->recvBuf, DB_BUFSIZ, 0);
    if (iResult > 0){
        this->recvDataSize = iResult;
        return(iResult);
    }else if (iResult == 0){
        return(0); // connection has been gracefully closed, which is unexpected
    }else{
        cout << "Error at recv(): " << WSAGetLastError() << endl;
        return(0);
    }
}

int FileDB::disconnect(){
    int iResult;
    // shutdown the connection for sending since no more data will be sent
    // the client can still use the ConnectSocket for receiving data
    iResult = shutdown(this->socket_tcp, SD_SEND);
    if(iResult == SOCKET_ERROR){
        cout << "Error at shutdown(): " << WSAGetLastError() << endl;
        closesocket(this->socket_tcp);
        WSACleanup();
        return 0;
    }
    return(1);
}

int FileDB::awknowlage(){
    int iResult = this->send("ok");
    return(iResult);
}

int FileDB::handshake(){
    int iResult;
    this->connect();
    if(this->status){
        string data = "handshake ";
        data.append(to_string(VERSION));
        // data.append(" keyhash ");
        // data.append(keyHash);
        this->send(data.c_str());
        this->disconnect();
        iResult = this->receive();
        if(!iResult){return(0);}
        int serverVersion = stoi(string(this->recvBuf, this->recvDataSize).c_str());
        if(serverVersion != VERSION){return(-1);}
        return(1);
    }
    return(0);
}

int FileDB::downloadMap(const string& map, const string& modID, int checkIfMapExists){
    int iResult;
    ofstream rfa_file;

    this->connect();
    if(this->status){
        string data = "downloadmap ";
        data.append(map);
        data.append(" ");
        data.append(modID);
        this->send(data.c_str());
        iResult = this->receive();
        if(!iResult){return(0);}
        int numFiles = stoi(string(this->recvBuf, this->recvDataSize).c_str());
//        cout << "numFiles: " << numFiles << endl;
        if(checkIfMapExists){ // when only checking if a map exists return 1 if numFiles is nonzero
            this->disconnect();
            return(numFiles > 0);
        }
        this->awknowlage();
        for (int i = 0; i < numFiles; ++i){
            iResult = this->receive();
            if(!iResult){return(0);}
            string fileName(this->recvBuf, this->recvDataSize);
            this->awknowlage();

            iResult = this->receive();
            if(!iResult){return(0);}
            int fileSize = stoi(string(this->recvBuf, this->recvDataSize).c_str());
            this->awknowlage();

            iResult = this->receive();
            if(!iResult){return(0);}
            unsigned long long last_modified = stoll(string(this->recvBuf, this->recvDataSize).c_str());
            this->awknowlage();

            cout << "downloading: " << fileName << endl;

            // check validity of name send:
            if(!(fileName.length() == map.length() || fileName.length() == map.length()+4) || strcicmp(fileName.c_str(), map.c_str()) != 0){
                cout << "Wrong file name send by server(): '" << fileName << "'" << endl;
                cout << "name: '" << map << "'" << endl;
                cout << "fileName.length(): '" << fileName.length() << "'" << endl;
                cout << "map.length(): '" << map.length() << "'" << endl;
                return 0;
            }


            string filePath = "mods/"+modID+"/archives/bf1942/levels/"+fileName+".rfa";
            rfa_file.open(filePath.c_str(), fstream::out|fstream::binary|fstream::trunc);
            if(!rfa_file.is_open()){
                cout << "Error at open(): " << filePath << endl;
                return 0;
            }
            int totalReceivedFileBytes = 0;
            int loadBarSize = 0;
            cout << std::string(20, '.') << endl;
            do{
                iResult = this->receive();
                if(!iResult){return(0);}
				totalReceivedFileBytes += iResult;
				rfa_file.write(this->recvBuf, iResult);
				int loadBarSize_new = totalReceivedFileBytes*20/fileSize;
				for(int j = 0; j < loadBarSize_new - loadBarSize; j++){
                    cout << "#";
				}
                loadBarSize = loadBarSize_new;
            } while (iResult > 0 && totalReceivedFileBytes < fileSize);
            cout << endl;
            this->awknowlage();
            rfa_file.close();
            fileSetWriteTime(filePath.c_str(), last_modified);
        }
        this->disconnect();
    }
    return(1);
}

int FileDB::update(){
    int iResult;
    ofstream updater_file;

    this->connect();
    if(this->status){
        string data = "update ";
        data.append(to_string(VERSION));
        this->send(data.c_str());

        iResult = this->receive();
        if(!iResult){return(0);}
        int fileSize = stoi(string(this->recvBuf, this->recvDataSize).c_str());
        this->awknowlage();

        cout << "downloading update:" << endl;

        string filePath = UPDATER_FILE;
        updater_file.open(filePath.c_str(), fstream::out|fstream::binary|fstream::trunc);
        if(!updater_file.is_open()){
            cout << "Error at open(): " << filePath << endl;
            return 0;
        }
        int totalReceivedFileBytes = 0;
        int loadBarSize = 0;
        cout << std::string(20, '.') << endl;
        do{
            iResult = this->receive();
            if(!iResult){return(0);}
            totalReceivedFileBytes += iResult;
            updater_file.write(this->recvBuf, iResult);
            int loadBarSize_new = totalReceivedFileBytes*20/fileSize;
            for(int j = 0; j < loadBarSize_new - loadBarSize; j++){
                cout << "#";
            }
            loadBarSize = loadBarSize_new;
        } while (iResult > 0 && totalReceivedFileBytes < fileSize);
        cout << endl;
        this->awknowlage();
        updater_file.close();
        this->disconnect();
        _spawnl(P_OVERLAY, UPDATER_FILE, UPDATER_FILE, NULL);
    }
    return(1);
}

