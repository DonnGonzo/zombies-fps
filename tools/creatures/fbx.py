# Minimal binary FBX reader: parse(path) gives the node tree as (name, properties, children) tuples.
import struct, zlib, sys
def parse(path):
    b=open(path,'rb').read(); ver=struct.unpack_from('<I',b,23)[0]; big=ver>=7500
    def node(o):
        if big: end,np_,pl=struct.unpack_from('<QQQ',b,o); o+=24
        else: end,np_,pl=struct.unpack_from('<III',b,o); o+=12
        nl=b[o]; o+=1
        if end==0: return None,o
        name=b[o:o+nl].decode('latin1'); o+=nl
        props=[]
        for _ in range(np_):
            t=chr(b[o]); o+=1
            if t=='Y': props.append(struct.unpack_from('<h',b,o)[0]); o+=2
            elif t=='C': props.append(b[o]); o+=1
            elif t=='I': props.append(struct.unpack_from('<i',b,o)[0]); o+=4
            elif t=='F': props.append(struct.unpack_from('<f',b,o)[0]); o+=4
            elif t=='D': props.append(struct.unpack_from('<d',b,o)[0]); o+=8
            elif t=='L': props.append(struct.unpack_from('<q',b,o)[0]); o+=8
            elif t in 'SR':
                n=struct.unpack_from('<I',b,o)[0]; o+=4; props.append(b[o:o+n].decode('latin1') if t=='S' else b[o:o+n]); o+=n
            elif t in 'fdlib':
                n,enc,cl=struct.unpack_from('<III',b,o); o+=12; raw=b[o:o+cl]; o+=cl
                props.append(('arr',t,n))
        kids=[]
        while o<end:
            k,o=node(o)
            if k is None: break
            kids.append(k)
        return (name,props,kids),end
    o=27; top=[]
    while o<len(b)-200:
        n,o=node(o)
        if n is None: break
        top.append(n)
    return top
