import fs from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import sharp from "sharp";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const output = path.resolve(root, "../Deliverables");
const report = {
  checkedAt: new Date().toISOString(),
  passed: true,
  images: [],
  videos: [],
  originalSaveRestored: null,
};
const assert = (condition, message) => {
  if (!condition) throw new Error(message);
};
const digest = (bytes) =>
  crypto.createHash("sha256").update(bytes).digest("hex");
const image = async (file, width, height, alpha) => {
  const absolute = path.join(output, file);
  const bytes = await fs.readFile(absolute);
  const meta = await sharp(bytes).metadata();
  assert(
    meta.width === width && meta.height === height,
    `Wrong dimensions: ${file}`,
  );
  assert(meta.format === "png", `Expected PNG: ${file}`);
  assert(meta.hasAlpha === alpha, `Wrong alpha mode: ${file}`);
  assert(meta.channels === (alpha ? 4 : 3), `Wrong channel count: ${file}`);
  assert(meta.depth === "uchar", `Expected 8-bit channels: ${file}`);
  if (file === "PlayStore/app-icon-512.png")
    assert(bytes.length <= 1024 * 1024, "App icon exceeds 1024 KB");
  report.images.push({
    file,
    width,
    height,
    mode: alpha ? "RGBA" : "RGB",
    bytes: bytes.length,
    sha256: digest(bytes),
  });
};
await image("PlayStore/app-icon-512.png", 512, 512, true);
await image("Masters/app-icon-1024.png", 1024, 1024, true);
await image("PlayStore/feature-graphic-1024x500.png", 1024, 500, false);
await image("Masters/banner-2048x1000.png", 2048, 1000, false);
const screenshots = (
  await fs.readdir(path.join(output, "PlayStore/Screenshots"))
)
  .filter((f) => f.endsWith(".png"))
  .sort();
assert(
  screenshots.length === 8,
  `Expected exactly 8 screenshots, found ${screenshots.length}`,
);
for (const file of screenshots)
  await image(`PlayStore/Screenshots/${file}`, 1080, 1920, false);
await image("Previews/screenshot-overview.png", 1464, 1272, false);
await image("Video/trailer-thumbnail-1920x1080.png", 1920, 1080, false);

const probe = path.join(
  root,
  "node_modules/@remotion/compositor-win32-x64-msvc/ffprobe.exe",
);
for (const [file, width, height] of [
  ["Video/riverlight-trailer-1920x1080.mp4", 1920, 1080],
  ["Video/riverlight-promo-1080x1920.mp4", 1080, 1920],
]) {
  const absolute = path.join(output, file);
  const result = spawnSync(
    probe,
    [
      "-v",
      "error",
      "-count_frames",
      "-show_entries",
      "format=duration:stream=codec_type,codec_name,width,height,r_frame_rate,pix_fmt,nb_read_frames,sample_rate,channels,color_space,color_range,color_transfer,color_primaries",
      "-of",
      "json",
      absolute,
    ],
    { encoding: "utf8", windowsHide: true, timeout: 180000 },
  );
  assert(
    result.status === 0,
    `Video inspection failed: ${file}: ${result.stderr}`,
  );
  assert(
    !result.stderr.trim(),
    `Video decode warnings: ${file}: ${result.stderr}`,
  );
  const meta = JSON.parse(result.stdout);
  const video = meta.streams.find((s) => s.codec_type === "video");
  const audio = meta.streams.find((s) => s.codec_type === "audio");
  assert(
    video?.width === width && video?.height === height,
    `Wrong video dimensions: ${file}`,
  );
  assert(
    video.codec_name === "h264" && video.pix_fmt === "yuv420p",
    `Wrong video format: ${file}`,
  );
  assert(video.r_frame_rate === "30/1", `Wrong frame rate: ${file}`);
  assert(video.color_space === 'bt709' && video.color_range === 'tv', `Expected standard Rec.709 video: ${file}`);
  assert(
    Number(video.nb_read_frames) === 900,
    `Expected 900 decoded frames: ${file}`,
  );
  assert(
    Math.abs(Number(meta.format.duration) - 30) < 0.05,
    `Wrong duration: ${file}`,
  );
  assert(audio?.codec_name === "aac", `Missing AAC audio: ${file}`);
  const bytes = await fs.readFile(absolute);
  report.videos.push({
    file,
    width,
    height,
    durationSeconds: Number(meta.format.duration),
    fps: 30,
    decodedFrames: Number(video.nb_read_frames),
    videoCodec: video.codec_name,
    pixelFormat: video.pix_fmt,
    colorSpace: video.color_space,
    colorRange: video.color_range,
    audioCodec: audio.codec_name,
    audioSampleRate: Number(audio.sample_rate),
    audioChannels: audio.channels,
    bytes: bytes.length,
    sha256: digest(bytes),
  });
}

const backup = path.resolve(root, "../../.utmp/campaign-save-original.json");
const personalSave = path.join(
  process.env.USERPROFILE,
  "AppData/LocalLow/CozyFishing/Riverlight/riverlight_save.json",
);
try {
  const [a, b] = await Promise.all([
    fs.readFile(backup),
    fs.readFile(personalSave),
  ]);
  report.originalSaveRestored = digest(a) === digest(b);
  assert(
    report.originalSaveRestored,
    "Original player save differs from its backup",
  );
} catch (error) {
  if (error.code !== "ENOENT") throw error;
}
await fs.writeFile(
  path.join(output, "validation-report.json"),
  JSON.stringify(report, null, 2),
);
console.log(
  `PASS: ${report.images.length} PNG assets, ${screenshots.length} phone screenshots, 2 complete 30-second H.264/AAC videos.`,
);
if (report.originalSaveRestored !== null)
  console.log(`Original save restored: ${report.originalSaveRestored}`);
