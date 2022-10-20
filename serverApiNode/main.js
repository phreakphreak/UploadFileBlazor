const fs = require("fs");
const path = require("path");
const util = require("util");
const Koa = require("koa");
const cors = require("@koa/cors");
const multer = require("@koa/multer");
const Router = require("@koa/router");
const serve = require("koa-static");
const fse = require("fs-extra");
const readdir = util.promisify(fs.readdir);
const unlink = util.promisify(fs.unlink);

const app = new Koa();
const router = new Router();
const TMP_DIR = path.join(__dirname, "tmp"); // temporary directory
const UPLOAD_DIR = path.join(__dirname, "public/upload");
const IGNORES = [".DS_Store"]; // ignored files

const storage = multer.diskStorage({
  destination: async function (req, file, cb) {
    let fileMd5 = file.originalname.split("-")[0];
    const fileDir = path.join(TMP_DIR, fileMd5);
    fse.ensureDirSync(fileDir);
    const flag = fse.pathExistsSync(fileDir);
    console.log("flag:", flag);
    if (flag) {
      cb(null, fileDir);
    }
  },
  filename: function (req, file, cb) {
    console.log(file);
    let chunkIndex = file.originalname.split("-")[1];

    cb(null, `${chunkIndex}`);
  },
});

const multerUpload = multer({ storage });

router.get("/", async (ctx) => {
  ctx.body = "Concurrent Download Demo";
});

// TODO:Endpoint
router.get("/upload/exists", async (ctx) => {
  const { name: fileName, md5: fileMd5 } = ctx.query;
  const filePath = path.join(UPLOAD_DIR, fileName);
  const isExists = await fse.pathExists(filePath);
  if (isExists) {
    ctx.body = {
      status: "success",
      data: {
        isExists: true,
        url: `http://localhost:3000/${fileName}`,
      },
    };
  } else {
    let chunkIds = [];
    const chunksPath = path.join(TMP_DIR, fileMd5);
    const hasChunksPath = await fse.pathExists(chunksPath);
    console.log(">>>> has chunk:", hasChunksPath);
    if (hasChunksPath) {
      let files = await readdir(chunksPath);
      console.log(">>>> files => ",files)
      chunkIds = files.filter((file) => {
        return IGNORES.indexOf(file) === -1;
      });
    }
    ctx.body = {
      status: "success",
      data: {
        isExists: false,
        chunkIds,
      },
    };
  }
});

// TODO:Endpoint
router.post(
  "/upload/single",
  multerUpload.single("file"),
  async (ctx, next) => {
    ctx.body = {
      code: 1,
      data: ctx.file.originalname,
    };
  }
);

// TODO:Endpoint
router.get("/upload/concatFiles", async (ctx) => {
  const { name: fileName, md5: fileMd5 } = ctx.query;
  await concatFiles(
    path.join(TMP_DIR, fileMd5),
    path.join(UPLOAD_DIR, fileName)
  );
  ctx.body = {
    status: "success",
    data: {
      url: `http://localhost:3000/${fileName}`,
    },
  };
});

async function concatFiles(sourceDir, targetPath) {
  const readFile = (file, ws) =>
    new Promise((resolve, reject) => {
      fs.createReadStream(file)
        .on("data", (data) => ws.write(data))
        .on("end", resolve)
        .on("error", reject);
    });
  const files = await readdir(sourceDir);
  const sortedFiles = files
    .filter((file) => {
      return IGNORES.indexOf(file) === -1;
    })
    .sort((a, b) => a - b);
  const writeStream = fs.createWriteStream(targetPath);
  for (const file of sortedFiles) {
    let filePath = path.join(sourceDir, file);
    await readFile(filePath, writeStream);
    await unlink(filePath); // remove merged chunks
    // await fse.remove(sourceDir); // remove temporary file directory
  }
  writeStream.end();
}

// register middleware
app.use(cors());
app.use(serve(UPLOAD_DIR));
app.use(router.routes()).use(router.allowedMethods());

app.listen(3000, async () => {
  await fse.ensureDir(UPLOAD_DIR);
  console.log("app starting at port http://localhost:3000");
});
