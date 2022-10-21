let AzureFileSystem = function(azureGateway) {
    let EMPTY_DIR_DUMMY_BLOB_NAME = "aspxAzureEmptyFolderBlob";

    let gateway = azureGateway;

    let getItems = function(path) {
        let prefix = getDirectoryBlobName(path);
        return gateway.getBlobList(prefix)
            .then(function(entries) {
                return getDataObjectsFromEntries(entries, prefix);
            });
    };

    let createDirectory = function(path, name) {
        let blobName = path ? path + "/" + name : name;
        return gateway.createDirectoryBlob(blobName);
    };

    let renameFile = function(path, name) {
        let newPath = getPathWithNewName(path, name);
        return moveFile(path, newPath);
    };

    let renameDirectory = function(path, name) {
        let newPath = getPathWithNewName(path, name);
        return moveDirectory(path, newPath);
    };

    let getPathWithNewName = function(path, name) {
        let parts = path.split("/");
        parts[parts.length - 1] = name;
        return parts.join("/");
    };

    let deleteFile = function(path) {
        return gateway.deleteBlob(path);
    };

    let deleteDirectory = function(path) {
        let prefix = getDirectoryBlobName(path);
        return executeActionForEachEntry(prefix, function(entry) {
            return gateway.deleteBlob(entry.name);
        });
    };

    let copyFile = function(sourcePath, destinationPath) {
        return gateway.copyBlob(sourcePath, destinationPath);
    };

    let copyDirectory = function(sourcePath, destinationPath) {
        let prefix = getDirectoryBlobName(sourcePath);
        let destinationKey = getDirectoryBlobName(destinationPath);
        return executeActionForEachEntry(prefix, function(entry) {
            return copyEntry(entry, prefix, destinationKey);
        });
    };

    let copyEntry = function(entry, sourceKey, destinationKey) {
        let restName = entry.name.substr(sourceKey.length);
        let newDestinationKey = destinationKey + restName;
        return gateway.copyBlob(entry.name, newDestinationKey);
    };

    let moveFile = function(sourcePath, destinationPath) {
        return gateway.copyBlob(sourcePath, destinationPath)
            .then(function() {
                gateway.deleteBlob(sourcePath);
            });
    };

    let moveDirectory = function(sourcePath, destinationPath) {
        let prefix = getDirectoryBlobName(sourcePath);
        let destinationKey = getDirectoryBlobName(destinationPath);
        return executeActionForEachEntry(prefix, function(entry) {
            return copyEntry(entry, prefix, destinationKey)
                .then(function() {
                    gateway.deleteBlob(entry.name);
                });
        });
    };

    let downloadFile = function(path) {
        gateway.getBlobUrl(path)
            .done(function(accessUrl) {
                window.location.href = accessUrl;
            });
    };

    let executeActionForEachEntry = function(prefix, action) {
        return gateway.getBlobList(prefix)
            .then(function(entries) {
                let deferreds = entries.map(function(entry) {
                    return action(entry);
                });
                return $.when.apply(null, deferreds);
            });
    };

    let getDataObjectsFromEntries = function(entries, prefix) {
        let result = [];
        let directories = {};

        entries.forEach(function(entry) {
            let restName = entry.name.substr(prefix.length);
            let parts = restName.split("/");

            if(parts.length === 1) {
                if(restName !== EMPTY_DIR_DUMMY_BLOB_NAME) {
                    let obj = {
                        name: restName,
                        isDirectory: false,
                        dateModified: entry.lastModified,
                        size: entry.length
                    };
                    result.push(obj);
                }
            } else {
                let dirName = parts[0];
                let directory = directories[dirName];
                if(!directory) {
                    directory = {
                        name: dirName,
                        isDirectory: true
                    };
                    directories[dirName] = directory;
                    result.push(directory);
                }

                if(!directory.hasSubDirectories) {
                    directory.hasSubDirectories = parts.length > 2;
                }
            }
        });

        result.sort(compareDataObjects);

        return result;
    };

    let compareDataObjects = function(obj1, obj2) {
        if(obj1.isDirectory === obj2.isDirectory) {
            let name1 = obj1.name.toLowerCase();
            let name2 = obj1.name.toLowerCase();
            if(name1 < name2) {
                return -1;
            } else {
                return name1 > name2 ? 1 : 0;
            }
        }

        return obj1.isDirectory ? -1 : 1;
    };

    let getDirectoryBlobName = function(path) {
        return path ? path + "/" : path;
    };

    return {
        getItems: getItems,
        createDirectory: createDirectory,
        renameFile: renameFile,
        renameDirectory: renameDirectory,
        deleteFile: deleteFile,
        deleteDirectory: deleteDirectory,
        copyFile: copyFile,
        copyDirectory: copyDirectory,
        moveFile: moveFile,
        moveDirectory: moveDirectory,
        downloadFile: downloadFile
    };
};

let AzureGateway = function(endpointUrl, onRequestExecuted) {

    let getBlobList = function(prefix) {
        return getAccessUrl("BlobList")
            .then(function(accessUrl) {
                return executeBlobListRequest(accessUrl, prefix);
            }).then(function(xml) {
                return parseEntryListResult(xml);
            });
    };

    let parseEntryListResult = function(xml) {
        return $(xml).find("Blob")
            .map(function(i, xmlEntry) {
                let entry = {};
                parseEntry($(xmlEntry), entry);
                return entry;
            })
            .get();
    };

    let parseEntry = function($xmlEntry, entry) {
        entry.etag = $xmlEntry.find("Etag").text();
        entry.name = $xmlEntry.find("Name").text();

        let dateStr = $xmlEntry.find("Last-Modified").text();
        entry.lastModified = new Date(dateStr);

        let lengthStr = $xmlEntry.find("Content-Length").text();
        entry.length = parseInt(lengthStr);
    };

    let executeBlobListRequest = function(accessUrl, prefix) {
        let params = {
            "restype": "container",
            "comp": "list"
        };
        if(prefix) {
            params.prefix = prefix;
        }
        return executeRequest(accessUrl, params);
    };

    let createDirectoryBlob = function(name) {
        return getAccessUrl("CreateDirectory", name)
            .then(function(accessUrl) {
                return executeRequest({
                    url: accessUrl,
                    method: "PUT",
                    headers: {
                        "x-ms-blob-type": "BlockBlob"
                    },
                    processData: false,
                    contentType: false
                });
            });
    };

    let deleteBlob = function(name) {
        return getAccessUrl("DeleteBlob", name)
            .then(function(accessUrl) {
                return executeRequest({
                    url: accessUrl,
                    method: "DELETE"
                });
            });
    };

    let copyBlob = function(sourceName, destinationName) {
        return getAccessUrl("CopyBlob", sourceName, destinationName)
            .then(function(accessUrl, accessUrl2) {
                return executeRequest({
                    url: accessUrl2,
                    method: "PUT",
                    headers: {
                        "x-ms-copy-source": accessUrl
                    }
                });
            });
    };

    let putBlock = function(uploadUrl, blockIndex, blockBlob) {
        let blockId = getBlockId(blockIndex);
        let params = {
            "comp": "block",
            "blockid": blockId
        };
        return executeRequest({
            url: uploadUrl,
            method: "PUT",
            data: blockBlob,
            processData: false,
            contentType: false
        }, params);
    };

    let putBlockList = function(uploadUrl, blockCount) {
        let content = getBlockListContent(blockCount);
        let params = {
            "comp": "blocklist"
        };
        return executeRequest({
            url: uploadUrl,
            method: "PUT",
            data: content
        }, params);
    };

    let getBlockListContent = function(blockCount) {
        let contentParts = [
            '<?xml version="1.0" encoding="utf-8"?>',
            '<BlockList>'
        ];

        for(let i = 0; i < blockCount; i += 1) {
            let blockContent = '  <Latest>' + getBlockId(i) + '</Latest>';
            contentParts.push(blockContent);
        }

        contentParts.push('</BlockList>');
        return contentParts.join('\n');
    };

    let getBlockId = function(blockIndex) {
        let res = blockIndex + "";
        while(res.length < 10) {
            res = "0" + res;
        }
        return btoa(res);
    };

    let getUploadAccessUrl = function(blobName) {
        return getAccessUrl("UploadBlob", blobName);
    };

    let getBlobUrl = function(blobName) {
        return getAccessUrl("GetBlob", blobName);
    };

    let getAccessUrl = function(command, blobName, blobName2) {
        let deferred = $.Deferred();
        let url = endpointUrl + "?command=" + command;
        if(blobName) {
            url += "&blobName=" + encodeURIComponent(blobName);
        }
        if(blobName2) {
            url += "&blobName2=" + encodeURIComponent(blobName2);
        }

        executeRequest(url)
            .done(function(result) {
                if(result.success) {
                    deferred.resolve(result.accessUrl, result.accessUrl2);
                } else {
                    deferred.reject(result.error);
                }
            })
            .fail(deferred.reject);

        return deferred.promise();
    };

    let executeRequest = function(args, commandParams) {
        let ajaxArgs = typeof args === "string" ? { url: args } : args;

        let method = ajaxArgs.method || "GET";

        let urlParts = ajaxArgs.url.split("?");
        let urlPath = urlParts[0];
        let restQueryString = urlParts[1];
        let commandQueryString = commandParams ? getQueryString(commandParams) : "";

        let queryString = commandQueryString || "";
        if(restQueryString) {
            queryString += queryString ? "&" + restQueryString : restQueryString;    
        }

        ajaxArgs.url = queryString ? urlPath + "?" + queryString : urlPath;

        return $.ajax(ajaxArgs)
            .done(function() {
                let eventArgs = {
                    method: method,
                    urlPath: urlPath,
                    queryString: queryString
                };
                if(onRequestExecuted) {
                    onRequestExecuted(eventArgs);
                }
            });
    };

    let getQueryString = function(params) {
        return Object.keys(params)
            .map(function(key) {
                return key + "=" + encodeURIComponent(params[key]);
            })
            .join("&");
    };

    return {
        getBlobList: getBlobList,
        createDirectoryBlob: createDirectoryBlob,
        deleteBlob: deleteBlob,
        copyBlob: copyBlob,
        putBlock: putBlock,
        putBlockList: putBlockList,
        getUploadAccessUrl: getUploadAccessUrl,
        getBlobUrl: getBlobUrl
    };
};
