"""Maya Quad Draw style keys for Blender's Poly Build tool.

Click empty space ........ place a point
Drag from a boundary edge  extrude a new quad
Drag an existing point ... move it
Ctrl + click ............. delete
Shift + click on points .. pick points; the 4th picked point makes a quad
                           (picking 3 and then Shift+clicking empty space makes a triangle)

Run inside Blender (Text editor > Run Script, or exec it). Safe to run more than once.
"""
import bpy
import bmesh
from bpy_extras import view3d_utils
from mathutils import Vector

PICK_RADIUS_PX = 18
KEYMAP_NAME = "3D View Tool: Edit Mesh, Poly Build"


def _facing_viewer(world_normal, rv3d):
    view_dir = rv3d.view_matrix.inverted().to_3x3() @ Vector((0.0, 0.0, 1.0))
    return world_normal.dot(view_dir) >= 0.0


def nearest_vertex(obj, bm, region, rv3d, mouse):
    """Closest front-facing vertex to the mouse, within PICK_RADIUS_PX, or None."""
    best, best_dist = None, PICK_RADIUS_PX
    normal_matrix = obj.matrix_world.to_3x3()
    for vert in bm.verts:
        if vert.hide:
            continue
        screen = view3d_utils.location_3d_to_region_2d(region, rv3d, obj.matrix_world @ vert.co)
        if screen is None:
            continue
        dist = (screen - mouse).length
        if dist < best_dist and _facing_viewer(normal_matrix @ vert.normal, rv3d):
            best, best_dist = vert, dist
    return best


def pick_vertex_or_make_face(obj, region, rv3d, mouse):
    """Returns a short status string. Mutates the edit-mesh selection / faces."""
    bm = bmesh.from_edit_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    vert = nearest_vertex(obj, bm, region, rv3d, mouse)
    selected = [v for v in bm.verts if v.select]

    if vert is None:
        if len(selected) == 3:
            return _make_face(obj, bm, "triangle")
        return "no point under the cursor"

    vert.select_set(not vert.select)
    selected = [v for v in bm.verts if v.select]
    if len(selected) == 4:
        return _make_face(obj, bm, "quad")
    bmesh.update_edit_mesh(obj.data)
    return f"{len(selected)} point(s) picked"


def _make_face(obj, bm, label):
    bmesh.update_edit_mesh(obj.data)
    result = bpy.ops.mesh.edge_face_add()
    bm = bmesh.from_edit_mesh(obj.data)
    for v in bm.verts:
        v.select_set(False)
    bm.select_flush(False)
    bmesh.update_edit_mesh(obj.data)
    return f"{label} created" if 'FINISHED' in result else f"could not make a {label}"


class RETOPO_OT_pick_make_face(bpy.types.Operator):
    bl_idname = "retopo.pick_make_face"
    bl_label = "Pick point / make face"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return context.mode == 'EDIT_MESH' and context.region_data is not None

    def invoke(self, context, event):
        mouse = Vector((event.mouse_region_x, event.mouse_region_y))
        status = pick_vertex_or_make_face(context.edit_object, context.region, context.region_data, mouse)
        self.report({'INFO'}, status)
        return {'FINISHED'}


def apply_keymap():
    km = bpy.context.window_manager.keyconfigs.user.keymaps[KEYMAP_NAME]
    for item in list(km.keymap_items):
        if item.idname in ("mesh.polybuild_face_at_cursor_move", "mesh.polybuild_face_at_cursor",
                           "mesh.polybuild_delete_at_cursor", "retopo.pick_make_face"):
            km.keymap_items.remove(item)
    for item in km.keymap_items:
        if item.idname == "mesh.polybuild_extrude_at_cursor_move":
            item.value = 'CLICK_DRAG'          # drag edge -> extrude, drag point -> move
    km.keymap_items.new("mesh.polybuild_face_at_cursor", 'LEFTMOUSE', 'CLICK')              # click: place point
    km.keymap_items.new("mesh.polybuild_delete_at_cursor", 'LEFTMOUSE', 'CLICK', ctrl=True)  # ctrl+click: delete
    km.keymap_items.new("retopo.pick_make_face", 'LEFTMOUSE', 'CLICK', shift=True)           # shift+click: make face


def register():
    try:
        bpy.utils.unregister_class(RETOPO_OT_pick_make_face)
    except (RuntimeError, ValueError):
        pass
    bpy.utils.register_class(RETOPO_OT_pick_make_face)
    apply_keymap()


register()
