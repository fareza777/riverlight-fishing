import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { bundle } from "@remotion/bundler";
import {
  getCompositions,
  openBrowser,
  renderMedia,
  renderStill,
} from "@remotion/renderer";
import sharp from "sharp";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const deliverables = path.resolve(root, "../Deliverables");
const mode = process.argv[2] ?? "stills";
const serveUrl = await bundle({
  entryPoint: path.join(root, "src/index.ts"),
  outDir: path.join(root, ".render-bundle"),
  publicDir: path.join(root, "public"),
  rspack: true,
  onProgress: () => {},
});
console.log("Campaign bundled.");
const browser = await openBrowser("chrome", { logLevel: "error" });
try {
  const compositions = await getCompositions(serveUrl, {
    puppeteerInstance: browser,
    logLevel: "error",
  });
  const get = (id) => {
    const c = compositions.find((c) => c.id === id);
    if (!c) throw new Error(`Missing composition ${id}`);
    return c;
  };
  const saveStill = async (id, file, alpha = false, frame = 0) => {
    const output = path.join(deliverables, file);
    await fs.mkdir(path.dirname(output), { recursive: true });
    const { buffer } = await renderStill({
      serveUrl,
      composition: get(id),
      frame,
      imageFormat: "png",
      puppeteerInstance: browser,
      logLevel: "error",
    });
    if (!buffer) throw new Error(`No image returned for ${id}`);
    const image = sharp(buffer).toColourspace("srgb");
    await (alpha ? image.ensureAlpha() : image.removeAlpha())
      .png({ compressionLevel: 9 })
      .toFile(output);
    console.log(`Saved ${file}`);
  };
  if (mode === "stills" || mode === "all") {
    await saveStill("App-Icon-512", "PlayStore/app-icon-512.png", true);
    await saveStill("App-Icon-Master", "Masters/app-icon-1024.png", true);
    await saveStill(
      "Feature-Graphic",
      "PlayStore/feature-graphic-1024x500.png",
    );
    await saveStill("Banner-Master", "Masters/banner-2048x1000.png");
    await saveStill(
      "Screenshot-01",
      "PlayStore/Screenshots/01-find-your-quiet.png",
    );
    await saveStill(
      "Screenshot-02",
      "PlayStore/Screenshots/02-feel-every-bite.png",
    );
    await saveStill("Screenshot-03", "PlayStore/Screenshots/03-48-reasons.png");
    await saveStill(
      "Screenshot-04",
      "PlayStore/Screenshots/04-four-waters.png",
    );
    await saveStill(
      "Screenshot-05",
      "PlayStore/Screenshots/05-chase-the-night.png",
    );
    await saveStill(
      "Screenshot-06",
      "PlayStore/Screenshots/06-his-last-adventure.png",
    );
    await saveStill("Screenshot-07", "PlayStore/Screenshots/07-your-haven.png");
    await saveStill(
      "Screenshot-08",
      "PlayStore/Screenshots/08-your-next-story.png",
    );
    await saveStill("Screenshot-Overview", "Previews/screenshot-overview.png");
    await saveStill(
      "Trailer-Thumbnail",
      "Video/trailer-thumbnail-1920x1080.png",
    );
  }
  if (mode === "samples" || mode === "all") {
    for (const [name, frame] of [
      ["hook", 64],
      ["cast", 150],
      ["reel", 240],
      ["collection", 330],
      ["worlds", 425],
      ["night", 530],
      ["memories", 630],
      ["cabin", 735],
      ["invitation", 858],
    ]) {
      await saveStill(
        "Riverlight-Trailer",
        `Previews/Video/landscape-${name}.png`,
        false,
        frame,
      );
      await saveStill(
        "Riverlight-Vertical",
        `Previews/Video/portrait-${name}.png`,
        false,
        frame,
      );
    }
  }
  if (mode === "videos" || mode === "all") {
    await fs.mkdir(path.join(deliverables, "Video"), { recursive: true });
    for (const [id, file] of [
      ["Riverlight-Trailer", "riverlight-trailer-1920x1080.mp4"],
      ["Riverlight-Vertical", "riverlight-promo-1080x1920.mp4"],
    ]) {
      let last = -1;
      await renderMedia({
        serveUrl,
        composition: get(id),
        outputLocation: path.join(deliverables, "Video", file),
        codec: "h264",
        crf: 18,
        pixelFormat: "yuv420p",
        colorSpace: "bt709",
        imageFormat: "jpeg",
        jpegQuality: 95,
        audioCodec: "aac",
        audioBitrate: "192k",
        x264Preset: "medium",
        concurrency: 2,
        puppeteerInstance: browser,
        logLevel: "error",
        overwrite: true,
        metadata: {
          title: "Riverlight — One old journal. Five living legends.",
          artist: "Riverlight",
          comment:
            "English campaign, actual game footage and original game audio.",
        },
        onProgress: ({ progress }) => {
          const p = Math.floor(progress * 10) * 10;
          if (p > last) {
            last = p;
            console.log(`${id}: ${p}%`);
          }
        },
      });
      console.log(`Saved Video/${file}`);
    }
  }
} finally {
  await browser.close({ silent: true });
}
