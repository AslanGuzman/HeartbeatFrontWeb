export function read(key) {
    return localStorage.getItem(key);
}

export function write(key, value) {
    localStorage.setItem(key, value);
}

export function remove(key) {
    localStorage.removeItem(key);
}
