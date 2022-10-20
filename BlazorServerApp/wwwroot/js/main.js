window.PlayAudioFile = () => {
  let blob = window.URL || window.webkitURL;
  let file = document.getElementById("fileMusic").files[0];
  let fileURL = blob.createObjectURL(file);
  console.log(file);
  console.log("File name: " + file.name);
  console.log("File type: " + file.type);
  console.log("File BlobURL: " + fileURL);
  document.getElementById("audio").src = fileURL;
};

window.RenderProgressBar = (count) => {
  const Progress = () =>
    React.createElement(
      Fabric.ProgressIndicator,
      {
        label: "React Counter",
        description: count,
        percentComplete: (count % 10) * 0.1,
      },
      null
    );

  ReactDOM.render(Progress(), document.getElementById("reactProgressBar"));
};

function calcFileMD5(file) {
  return new Promise((resolve, reject) => {
    // TODO: Se define el Size del Chunk
    let chunkSize = 2097152; // 2M
    let chunks = Math.ceil(file.size / chunkSize);
    let currentChunk = 0;
    let spark = new SparkMD5.ArrayBuffer();
    let fileReader = new FileReader();

    fileReader.onload = (e) => {
      spark.append(e.target.result);
      currentChunk++;
      if (currentChunk < chunks) {
        loadNext();
      } else {
        resolve(spark.end());
      }
    };

    fileReader.onerror = (e) => {
      reject(fileReader.error);
      reader.abort();
    };

    function loadNext() {
      let start = currentChunk * chunkSize,
        end = start + chunkSize >= file.size ? file.size : start + chunkSize;
      fileReader.readAsArrayBuffer(file.slice(start, end));
    }
    loadNext();
  });
}

async function asyncPool(concurrency, iterable, iteratorFn) {
  const ret = []; // Store all asynchronous tasks
  const executing = new Set(); // Stores executing asynchronous tasks
  for (const item of iterable) {
    // Call the iteratorFn function to create an asynchronous task
    const p = Promise.resolve().then(() => iteratorFn(item, iterable));
    
    ret.push(p); // save new async task
    executing.add(p); // Save an executing asynchronous task
    
    const clean = () => executing.delete(p);
    p.then(clean).catch(clean);
    if (executing.size >= concurrency) {
      // Wait for faster task execution to complete 
      await Promise.race(executing);
    }
  }
  return Promise.all(ret);
}
function checkFileExist(url, name, md5) {
  return request
    .get(url, {
      params: {
        name,
        md5,
      },
    })
    .then((response) => response.data);
}

const request = axios.create({
  baseURL: "http://localhost:3000/upload",
  // timeout: 30000,
});

function upload({
  url,
  file,
  fileMd5,
  fileSize,
  chunkSize,
  chunkIds,
  poolLimit = 1,
}) {
  const chunks =
    typeof chunkSize === "number" ? Math.ceil(fileSize / chunkSize) : 1;
  return asyncPool(poolLimit, [...new Array(chunks).keys()], (i) => {
    if (chunkIds.indexOf(i + "") !== -1) {
      // Ignore uploaded chunks
      return Promise.resolve();
    }
    let start = i * chunkSize;
    let end = i + 1 == chunks ? fileSize : (i + 1) * chunkSize;
    const chunk = file.slice(start, end);
    return uploadChunk({
      url,
      chunk,
      chunkIndex: i,
      fileMd5,
      fileName: file.name,
    });
  });
}
function uploadChunk({ url, chunk, chunkIndex, fileMd5, fileName }) {
  let formData = new FormData();
  formData.set("file", chunk, fileMd5 + "-" + chunkIndex);
  formData.set("name", fileName);
  formData.set("timestamp", Date.now());
  console.log(">>> formData: ",formData)
  let config = {
    onUploadProgress: function(progressEvent) {
      document.getElementById("status").innerHTML = `<p>${Math.round( (progressEvent.loaded * 100) / progressEvent.total )}</p>`
      window.RenderProgressBar(Math.round( (progressEvent.loaded * 100) / progressEvent.total ));
    }
  };
  return request.post(url, formData,config)
}

function concatFiles(url, name, md5) {
  return request.get(url, {
    params: {
      name,
      md5,
    },
  });
}
window.uploadFile= async()=> {
  let uploadFileEle =  document.getElementById("uploadFile")
  if (!uploadFileEle.files.length) return;
  const file = uploadFileEle.files[0];
  const fileMd5 = await calcFileMD5(file); // Calculate the MD5 of the file
  const fileStatus = await checkFileExist(
    // Check if the file already exists
    "/exists",
    file.name,
    fileMd5
  );
  if (fileStatus.data && fileStatus.data.isExists) {
    alert("File has been uploaded");
    return;
  } else {
    const singleFile = {
      url: "/single",
      file,
      fileMd5,
      fileSize: file.size,
      chunkSize: 1 * 1024 * 1024,
      chunkIds: fileStatus.data.chunkIds,
      poolLimit: 3,
    }
    console.log("Single Chunk: ",singleFile, +new Date())
    const response = await upload(singleFile);
    console.log("Response: ",response)
  }
  const fileData = await concatFiles("/concatFiles", file.name, fileMd5);
  const {
    data: {
      data: { url },
    },
  } = fileData;
  alert(`Uploaded file url is: ${url}`);
}

