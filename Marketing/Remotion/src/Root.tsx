import React from "react";
import { Composition, Folder, Still } from "remotion";
import { RiverlightTrailer } from "./Trailer";
import { AppIcon, FeatureGraphic, TrailerPoster } from "./StoreAssets";
import { ContactSheet, StoreScreenshot } from "./StoreScreenshots";
import { HookScene } from "./scenes/HookScene";
import {
  CastScene,
  ReelScene,
  CollectionScene,
  NightScene,
  CabinScene,
} from "./scenes/PlayScenes";
import { WorldsScene } from "./scenes/WorldsScene";
import { LegacyScene } from "./scenes/LegacyScene";
import { EndScene } from "./scenes/EndScene";

export const RemotionRoot: React.FC = () => {
  return (
    <>
      <Composition
        id="Riverlight-Trailer"
        component={RiverlightTrailer}
        width={1920}
        height={1080}
        fps={30}
        durationInFrames={900}
      />
      <Composition
        id="Riverlight-Vertical"
        component={RiverlightTrailer}
        width={1080}
        height={1920}
        fps={30}
        durationInFrames={900}
      />
      <Folder name="Play-Store">
        <Still id="App-Icon-512" component={AppIcon} width={512} height={512} />
        <Still
          id="App-Icon-Master"
          component={AppIcon}
          width={1024}
          height={1024}
        />
        <Still
          id="Feature-Graphic"
          component={FeatureGraphic}
          width={1024}
          height={500}
        />
        <Still
          id="Banner-Master"
          component={FeatureGraphic}
          width={2048}
          height={1000}
        />
        <Still
          id="Screenshot-01"
          component={StoreScreenshot}
          width={1080}
          height={1920}
          defaultProps={{ index: 1 }}
        />
        <Still
          id="Screenshot-02"
          component={StoreScreenshot}
          width={1080}
          height={1920}
          defaultProps={{ index: 2 }}
        />
        <Still
          id="Screenshot-03"
          component={StoreScreenshot}
          width={1080}
          height={1920}
          defaultProps={{ index: 3 }}
        />
        <Still
          id="Screenshot-04"
          component={StoreScreenshot}
          width={1080}
          height={1920}
          defaultProps={{ index: 4 }}
        />
        <Still
          id="Screenshot-05"
          component={StoreScreenshot}
          width={1080}
          height={1920}
          defaultProps={{ index: 5 }}
        />
        <Still
          id="Screenshot-06"
          component={StoreScreenshot}
          width={1080}
          height={1920}
          defaultProps={{ index: 6 }}
        />
        <Still
          id="Screenshot-07"
          component={StoreScreenshot}
          width={1080}
          height={1920}
          defaultProps={{ index: 7 }}
        />
        <Still
          id="Screenshot-08"
          component={StoreScreenshot}
          width={1080}
          height={1920}
          defaultProps={{ index: 8 }}
        />
        <Still
          id="Screenshot-Overview"
          component={ContactSheet}
          width={1464}
          height={1272}
        />
        <Still
          id="Trailer-Thumbnail"
          component={TrailerPoster}
          width={1920}
          height={1080}
        />
      </Folder>
      <Folder name="Editable-Scenes">
        <Composition
          id="Hook"
          component={HookScene}
          width={1920}
          height={1080}
          fps={30}
          durationInFrames={90}
        />
        <Composition
          id="Cast"
          component={CastScene}
          width={1920}
          height={1080}
          fps={30}
          durationInFrames={90}
        />
        <Composition
          id="Reel"
          component={ReelScene}
          width={1920}
          height={1080}
          fps={30}
          durationInFrames={90}
        />
        <Composition
          id="Collection"
          component={CollectionScene}
          width={1920}
          height={1080}
          fps={30}
          durationInFrames={90}
        />
        <Composition
          id="Four-Waters"
          component={WorldsScene}
          width={1920}
          height={1080}
          fps={30}
          durationInFrames={120}
        />
        <Composition
          id="Night"
          component={NightScene}
          width={1920}
          height={1080}
          fps={30}
          durationInFrames={90}
        />
        <Composition
          id="Memories"
          component={LegacyScene}
          width={1920}
          height={1080}
          fps={30}
          durationInFrames={120}
        />
        <Composition
          id="Cabin"
          component={CabinScene}
          width={1920}
          height={1080}
          fps={30}
          durationInFrames={90}
        />
        <Composition
          id="Final-Invitation"
          component={EndScene}
          width={1920}
          height={1080}
          fps={30}
          durationInFrames={120}
        />
      </Folder>
    </>
  );
};
