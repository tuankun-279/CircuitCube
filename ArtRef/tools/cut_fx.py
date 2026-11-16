"""Cuts glow art drawn on pure black into white-free RGBA sprites (alpha from brightness, colour un-premultiplied)."""
import sys
import numpy as np
from PIL import Image

def glow_to_rgba(a, floor=10):
    a = a.astype(np.float32)
    m = a.max(axis=2)
    alpha = np.clip((m - floor) / (255 - floor), 0, 1)
    alpha = alpha ** 0.85          # keep the soft glow visible
    rgb = np.where(m[..., None] > 0, a / np.maximum(m[..., None], 1) * 255, 0)
    out = np.dstack([rgb, alpha * 255]).clip(0, 255).astype(np.uint8)
    return out

def crop(img, box, pad=14):
    x0, y0, x1, y1 = box
    return img[max(0, y0 - pad):y1 + pad, max(0, x0 - pad):x1 + pad]

def save(arr, path):
    Image.fromarray(arr, 'RGBA').save(path)
