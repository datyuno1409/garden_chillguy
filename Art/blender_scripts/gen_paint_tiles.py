"""Generate painterly rock tiles with Stable Diffusion 1.5 + IP-Adapter (runs in E:\\AI\\paintgen).

    E:\\AI\\paintgen\\Scripts\\python.exe gen_paint_tiles.py <out_dir> <reference.png> [tiles_per_style] [steps]

The reference painting only sets the *style* (brush marks, flaky dabs, colour mood) through IP-Adapter; the tiles are new
images. They are not tileable by themselves: rock_paint.py samples them with mirror-repeat, which has no visible seam.
"""
import sys
import time
from pathlib import Path

import torch
from diffusers import DPMSolverMultistepScheduler, StableDiffusionPipeline
from PIL import Image

MODEL = "stable-diffusion-v1-5/stable-diffusion-v1-5"
SIZE = 512
# crops of the reference (x0, y0, x1, y1) in a 1200 x 1076 image: lit face and shadowed side
CROPS = {"lit": (260, 170, 780, 590), "shade": (840, 260, 1040, 600)}
PROMPT = ("extreme close-up of a rough stone surface filling the whole frame, digital painting, flat brush strokes, "
          "angular flaky dabs of paint, soft even light, no objects")
NEGATIVE = ("pebbles, cobblestones, round stones, pile of stones, gaps, black, dark background, dark spots, "
            "high contrast, harsh shadows, text, signature, watermark, border, frame, perspective, photo, 3d render, "
            "plant, moss, grass")


def main(argv):
    out, reference = Path(argv[1]), Image.open(argv[2]).convert("RGB")
    per_style = int(argv[3]) if len(argv) > 3 else 2
    steps = int(argv[4]) if len(argv) > 4 else 28
    out.mkdir(parents=True, exist_ok=True)
    t0 = time.time()
    pipe = StableDiffusionPipeline.from_pretrained(MODEL, torch_dtype=torch.float16, variant="fp16",
                                                   safety_checker=None, requires_safety_checker=False)
    pipe.load_ip_adapter("h94/IP-Adapter", subfolder="models", weight_name="ip-adapter_sd15.bin")
    pipe.set_ip_adapter_scale(0.8)
    pipe.scheduler = DPMSolverMultistepScheduler.from_config(pipe.scheduler.config, use_karras_sigmas=True)
    pipe.to("cuda")                      # no attention slicing: it would replace the IP-Adapter attention processors
    print(f"model ready in {time.time() - t0:.0f}s", flush=True)
    for style, box in CROPS.items():
        style_image = reference.crop(box)
        for i in range(per_style):
            gen = torch.Generator("cuda").manual_seed(2000 + i * 17 + len(style))
            t = time.time()
            image = pipe(PROMPT, negative_prompt=NEGATIVE, ip_adapter_image=style_image, num_inference_steps=steps,
                         guidance_scale=6.0, width=SIZE, height=SIZE, generator=gen).images[0]
            path = out / f"tile_{style}_{i}.png"
            image.save(path)
            print(f"{path.name}: {time.time() - t:.0f}s", flush=True)
    print("DONE", flush=True)


if __name__ == "__main__":
    main(sys.argv)
